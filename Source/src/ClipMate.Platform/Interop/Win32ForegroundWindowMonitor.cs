using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using Microsoft.Extensions.Logging;

namespace ClipMate.Platform.Interop;

/// <summary>
/// <see cref="IForegroundWindowMonitor" /> backed by an out-of-context <c>EVENT_SYSTEM_FOREGROUND</c> WinEvent hook.
/// The hook lives on a dedicated thread with its own message loop, so it does not depend on which thread starts it
/// or on the UI thread staying responsive.
/// </summary>
public sealed class Win32ForegroundWindowMonitor : IForegroundWindowMonitor
{
    private const uint _eventSystemForeground = 0x0003;
    private const uint _wineventOutOfContext = 0x0000;
    private const uint _wmQuit = 0x0012;

    private readonly ILogger<Win32ForegroundWindowMonitor> _logger;
    private readonly Lock _lock = new();

    // Kept in a field: the hook calls it from native code, so the delegate must outlive the hook
    private readonly WINEVENTPROC _callback;
    private Thread? _thread;
    private uint _threadId;
    private bool _disposed;

    public Win32ForegroundWindowMonitor(ILogger<Win32ForegroundWindowMonitor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _callback = OnWinEvent;
    }

    /// <inheritdoc />
    public event Action<HWND>? ForegroundChanged;

    /// <inheritdoc />
    public void Start()
    {
        lock (_lock)
        {
            if (_thread != null || _disposed)
                return;

            using var started = new ManualResetEventSlim();
            _thread = new Thread(() => Run(started))
            {
                IsBackground = true,
                Name = "Foreground window monitor",
            };

            _thread.Start();
            started.Wait();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            _disposed = true;
            if (_threadId != 0)
                PInvoke.PostThreadMessage(_threadId, _wmQuit, default, default);
        }

        _thread?.Join(TimeSpan.FromSeconds(2));
    }

    private void Run(ManualResetEventSlim started)
    {
        _threadId = PInvoke.GetCurrentThreadId();
        var hook = PInvoke.SetWinEventHook(_eventSystemForeground, _eventSystemForeground, HMODULE.Null, _callback, 0, 0,
            _wineventOutOfContext);
        started.Set();

        if (hook.IsNull)
        {
            _logger.LogError("Could not hook foreground window changes; the Paste Target will not follow the active window");
            return;
        }

        _logger.LogDebug("Foreground window monitor started");
        try
        {
            // Out-of-context WinEvents are delivered through this thread's message queue
            while (PInvoke.GetMessage(out var msg, HWND.Null, 0, 0) > 0)
            {
                PInvoke.TranslateMessage(msg);
                PInvoke.DispatchMessage(msg);
            }
        }
        finally
        {
            PInvoke.UnhookWinEvent(hook);
        }
    }

    private void OnWinEvent(HWINEVENTHOOK hWinEventHook, uint eventType, HWND hwnd, int idObject, int idChild,
        uint idEventThread, uint dwmsEventTime)
    {
        if (hwnd.IsNull)
            return;

        try
        {
            ForegroundChanged?.Invoke(hwnd);
        }
        catch (Exception ex)
        {
            // An exception must not escape into the native callback
            _logger.LogError(ex, "Error handling a foreground window change");
        }
    }
}
