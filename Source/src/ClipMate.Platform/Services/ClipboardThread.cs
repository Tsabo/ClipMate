using System.Windows.Interop;
using System.Windows.Threading;
using Windows.Win32.Foundation;

namespace ClipMate.Platform.Services;

/// <summary>
/// The dedicated clipboard thread: an STA thread with a message-only window and its own dispatcher, so
/// clipboard notifications are handled and the clipboard read without involving the UI thread. Code on the UI
/// thread only posts to it or awaits it, never waits on it synchronously.
/// </summary>
internal sealed class ClipboardThread
{
    private readonly TaskCompletionSource _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _stopping;
    private HwndSource? _window;

    private ClipboardThread(Dispatcher dispatcher, HwndSource window)
    {
        Dispatcher = dispatcher;
        _window = window;
        Handle = new HWND(window.Handle);
    }

    public Dispatcher Dispatcher { get; }

    /// <summary>
    /// The message-only window that receives the clipboard notifications.
    /// </summary>
    public HWND Handle { get; }

    /// <summary>
    /// Starts the thread and creates its window, which hands every message to <paramref name="hook" />.
    /// </summary>
    public static Task<ClipboardThread> StartAsync(HwndSourceHook hook)
    {
        var started = new TaskCompletionSource<ClipboardThread>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            ClipboardThread clipboardThread;
            try
            {
                var window = new HwndSource(new HwndSourceParameters("ClipMate.ClipboardMonitor")
                {
                    ParentWindow = new IntPtr(-3), // HWND_MESSAGE
                    WindowStyle = 0,
                });
                window.AddHook(hook);
                clipboardThread = new ClipboardThread(Dispatcher.CurrentDispatcher, window);
            }
            catch (Exception ex)
            {
                started.SetException(ex);
                return;
            }

            started.SetResult(clipboardThread);
            try
            {
                Dispatcher.Run();
            }
            finally
            {
                clipboardThread._exited.SetResult();
            }
        })
        {
            IsBackground = true,
            Name = "Clipboard",
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return started.Task;
    }

    /// <summary>
    /// Runs <paramref name="teardown" /> on the thread, destroys the window and ends the thread.
    /// </summary>
    public async Task StopAsync(Action teardown)
    {
        BeginStop(teardown);
        await _exited.Task.ConfigureAwait(false);
    }

    /// <summary>
    /// Like <see cref="StopAsync" />, without waiting for the thread to end.
    /// </summary>
    public void BeginStop(Action teardown)
    {
        if (Interlocked.Exchange(ref _stopping, 1) != 0)
            return;

        Dispatcher.InvokeAsync(() =>
        {
            try
            {
                teardown();
            }
            finally
            {
                _window?.Dispose();
                _window = null;
                Dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            }
        });
    }
}
