using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace ClipMate.UITests.Fixtures;

/// <summary>
/// A small window of the test process brought to the foreground, so the test can paste as "the application the user
/// is working in": reading the clipboard with this window passed to OpenClipboard is what an editor's Ctrl+V does.
/// It runs on its own STA thread with a dispatcher.
/// </summary>
public sealed partial class ForegroundTestWindow : IDisposable
{
    private const byte _vkMenu = 0x12;
    private const uint _keyEventKeyUp = 0x0002;

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
        return _dispatcher.Invoke(() => SystemClipboard.TryReadText(Handle));
    }

    private static Task BringToForegroundAsync(IntPtr handle) =>
        // Windows only lets the process that received the last input take the foreground; a synthetic Alt press
        // counts as that input
        Poll.UntilAsync(() =>
        {
            if (GetForegroundWindow() == handle)
                return true;

            keybd_event(_vkMenu, 0, 0, UIntPtr.Zero);
            keybd_event(_vkMenu, 0, _keyEventKeyUp, UIntPtr.Zero);
            SetForegroundWindow(handle);
            return GetForegroundWindow() == handle;
        }, "the test window to come to the foreground");

    public void Dispose()
    {
        _dispatcher.InvokeShutdown();
        _thread.Join();
    }

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    private static partial void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
}
