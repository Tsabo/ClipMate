using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace ClipMate.UITests.Fixtures;

/// <summary>
/// A small window of the test process brought to the foreground, so the test can paste as "the application the user
/// is working in": reading the clipboard with this window passed to OpenClipboard is what an editor's Ctrl+V does.
/// It runs on its own STA thread with a dispatcher.
/// </summary>
public sealed class ForegroundTestWindow : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly Thread _thread;

    private ForegroundTestWindow(Dispatcher dispatcher, Thread thread, IntPtr handle)
    {
        _dispatcher = dispatcher;
        _thread = thread;
        Handle = handle;
    }

    public IntPtr Handle { get; }

    /// <summary>
    /// Opens the window and waits until Windows reports it as the foreground window.
    /// </summary>
    public static async Task<ForegroundTestWindow> OpenAsync()
    {
        var ready = new TaskCompletionSource<(Dispatcher, IntPtr)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var window = new Window { Title = "ClipMate UI test paste target", Width = 300, Height = 120, Topmost = true };
            window.Show();
            ready.SetResult((Dispatcher.CurrentDispatcher, new WindowInteropHelper(window).Handle));
            Dispatcher.Run();
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        var (dispatcher, handle) = await ready.Task;
        await BringToForegroundAsync(handle);

        return new ForegroundTestWindow(dispatcher, thread, handle);
    }

    /// <summary>
    /// Reads the clipboard's text as a paste into this window, bringing it back to the foreground first.
    /// </summary>
    public async Task<string?> PasteAsync()
    {
        await BringToForegroundAsync(Handle);
        return Paste();
    }

    /// <summary>
    /// Reads the clipboard's text as a paste into this window, which must still be in the foreground.
    /// </summary>
    public string? Paste() => _dispatcher.Invoke(() => SystemClipboard.TryReadText(Handle));

    /// <summary>
    /// Puts a copy on the clipboard from this window's thread, so the clipboard owner (and the Source Application ClipMate
    /// records) is the test program until the window closes. The data object is created on that thread.
    /// </summary>
    public void Copy(Func<DataObject> create) => _dispatcher.Invoke(() => Clipboard.SetDataObject(create(), true));

    private static Task BringToForegroundAsync(IntPtr handle) => Foreground.ActivateAsync(handle, "the test window");

    public void Dispose()
    {
        _dispatcher.InvokeShutdown();
        _thread.Join();
    }
}
