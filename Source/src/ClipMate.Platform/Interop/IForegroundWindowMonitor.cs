using Windows.Win32.Foundation;

namespace ClipMate.Platform.Interop;

/// <summary>
/// Reports every change of the system's foreground window, ClipMate's own windows included.
/// </summary>
public interface IForegroundWindowMonitor : IDisposable
{
    /// <summary>
    /// Raised with the new foreground window, on the monitor's own thread.
    /// </summary>
    event Action<HWND>? ForegroundChanged;

    /// <summary>
    /// Starts reporting changes. Calling it again has no effect.
    /// </summary>
    void Start();
}
