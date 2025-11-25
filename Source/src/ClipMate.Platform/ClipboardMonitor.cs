using System.Windows;
using System.Windows.Interop;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace ClipMate.Platform;

/// <summary>
/// Monitors Windows clipboard changes using WM_CLIPBOARDUPDATE messages.
/// </summary>
public class ClipboardMonitor : IDisposable
{
    private HwndSource? _hwndSource;
    private bool _isMonitoring;
    private bool _disposed;
    private uint _lastSequenceNumber;

    /// <summary>
    /// Occurs when the clipboard content changes.
    /// </summary>
    public event EventHandler? ClipboardChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClipboardMonitor"/> class.
    /// </summary>
    public ClipboardMonitor()
    {
        _lastSequenceNumber = PInvoke.GetClipboardSequenceNumber();
    }

    /// <summary>
    /// Gets a value indicating whether the monitor is currently active.
    /// </summary>
    public bool IsMonitoring => _isMonitoring;

    /// <summary>
    /// Starts monitoring clipboard changes.
    /// </summary>
    /// <param name="window">The WPF window to use for receiving messages.</param>
    public void Start(Window window)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        if (_isMonitoring)
        {
            return;
        }

        if (window == null)
        {
            throw new ArgumentNullException(nameof(window));
        }

        // Get the window handle
        var windowInteropHelper = new WindowInteropHelper(window);
        var hwnd = windowInteropHelper.Handle;

        if (hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("Window handle is not available. Ensure the window is loaded.");
        }

        // Create HwndSource to intercept Windows messages
        _hwndSource = HwndSource.FromHwnd(hwnd);
        if (_hwndSource == null)
        {
            throw new InvalidOperationException("Failed to create HwndSource from window handle.");
        }

        // Add hook to process messages
        _hwndSource.AddHook(WndProc);

        // Register for clipboard updates
        if (!PInvoke.AddClipboardFormatListener(new HWND(hwnd)))
        {
            _hwndSource.RemoveHook(WndProc);
            throw new InvalidOperationException("Failed to register clipboard format listener.");
        }

        _isMonitoring = true;
    }

    /// <summary>
    /// Stops monitoring clipboard changes.
    /// </summary>
    public void Stop()
    {
        if (!_isMonitoring || _hwndSource == null)
        {
            return;
        }

        var hwnd = _hwndSource.Handle;
        
        // Unregister clipboard listener
        if (hwnd != IntPtr.Zero)
        {
            PInvoke.RemoveClipboardFormatListener(new HWND(hwnd));
        }

        // Remove message hook
        _hwndSource.RemoveHook(WndProc);
        _hwndSource = null;

        _isMonitoring = false;
    }

    /// <summary>
    /// Window procedure to handle Windows messages.
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_CLIPBOARDUPDATE = 0x031D;
        
        if (msg == WM_CLIPBOARDUPDATE)
        {
            var currentSequenceNumber = PInvoke.GetClipboardSequenceNumber();
            
            // Only raise event if sequence number actually changed
            if (currentSequenceNumber != _lastSequenceNumber)
            {
                _lastSequenceNumber = currentSequenceNumber;
                OnClipboardChanged();
            }
            
            handled = true;
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Raises the ClipboardChanged event.
    /// </summary>
    protected virtual void OnClipboardChanged()
    {
        ClipboardChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Gets the current clipboard sequence number.
    /// </summary>
    /// <returns>The sequence number.</returns>
    public uint GetSequenceNumber()
    {
        return PInvoke.GetClipboardSequenceNumber();
    }

    /// <summary>
    /// Disposes the clipboard monitor.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
