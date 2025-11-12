using System.Windows;
using System.Windows.Interop;
using ClipMate.Platform.Win32;

namespace ClipMate.Platform;

/// <summary>
/// Helper class for DPI awareness and scaling calculations.
/// </summary>
public static class DpiHelper
{
    private const double DefaultDpi = 96.0;
    private static bool _isPerMonitorDpiAware;

    /// <summary>
    /// Initializes DPI awareness for the application.
    /// </summary>
    public static void InitializeDpiAwareness()
    {
        try
        {
            // Try to set per-monitor DPI awareness (Windows 8.1+)
            var result = Win32Methods.SetProcessDpiAwareness(Win32Constants.PROCESS_PER_MONITOR_DPI_AWARE);
            _isPerMonitorDpiAware = result == 0;
        }
        catch
        {
            // Fall back to system DPI awareness (Windows Vista+)
            try
            {
                Win32Methods.SetProcessDPIAware();
                _isPerMonitorDpiAware = false;
            }
            catch
            {
                // DPI awareness is not supported
                _isPerMonitorDpiAware = false;
            }
        }
    }

    /// <summary>
    /// Gets the DPI for a specific window.
    /// </summary>
    /// <param name="window">The window to get DPI for.</param>
    /// <returns>The DPI value.</returns>
    public static int GetDpiForWindow(Window window)
    {
        if (window == null)
        {
            throw new ArgumentNullException(nameof(window));
        }

        var windowInteropHelper = new WindowInteropHelper(window);
        var hwnd = windowInteropHelper.Handle;

        if (hwnd == IntPtr.Zero)
        {
            return (int)DefaultDpi;
        }

        try
        {
            var dpi = Win32Methods.GetDpiForWindow(hwnd);
            return dpi > 0 ? dpi : (int)DefaultDpi;
        }
        catch
        {
            return (int)DefaultDpi;
        }
    }

    /// <summary>
    /// Gets the DPI scale factor for a window (1.0 = 96 DPI, 1.5 = 144 DPI, etc.).
    /// </summary>
    /// <param name="window">The window to get scale factor for.</param>
    /// <returns>The scale factor.</returns>
    public static double GetScaleFactor(Window window)
    {
        var dpi = GetDpiForWindow(window);
        return dpi / DefaultDpi;
    }

    /// <summary>
    /// Converts device-independent pixels to physical pixels.
    /// </summary>
    /// <param name="dipValue">The value in device-independent pixels.</param>
    /// <param name="scaleFactor">The DPI scale factor.</param>
    /// <returns>The value in physical pixels.</returns>
    public static double DipToPhysical(double dipValue, double scaleFactor)
    {
        return dipValue * scaleFactor;
    }

    /// <summary>
    /// Converts physical pixels to device-independent pixels.
    /// </summary>
    /// <param name="physicalValue">The value in physical pixels.</param>
    /// <param name="scaleFactor">The DPI scale factor.</param>
    /// <returns>The value in device-independent pixels.</returns>
    public static double PhysicalToDip(double physicalValue, double scaleFactor)
    {
        return physicalValue / scaleFactor;
    }

    /// <summary>
    /// Gets the primary screen width in device-independent pixels.
    /// </summary>
    /// <returns>The screen width.</returns>
    public static double GetScreenWidth()
    {
        var physicalWidth = Win32Methods.GetSystemMetrics(Win32Methods.SM_CXSCREEN);
        var source = PresentationSource.FromVisual(Application.Current?.MainWindow);
        var scaleFactor = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
        return physicalWidth * scaleFactor;
    }

    /// <summary>
    /// Gets the primary screen height in device-independent pixels.
    /// </summary>
    /// <returns>The screen height.</returns>
    public static double GetScreenHeight()
    {
        var physicalHeight = Win32Methods.GetSystemMetrics(Win32Methods.SM_CYSCREEN);
        var source = PresentationSource.FromVisual(Application.Current?.MainWindow);
        var scaleFactor = source?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;
        return physicalHeight * scaleFactor;
    }

    /// <summary>
    /// Gets a value indicating whether the application is per-monitor DPI aware.
    /// </summary>
    public static bool IsPerMonitorDpiAware => _isPerMonitorDpiAware;

    /// <summary>
    /// Gets the system DPI.
    /// </summary>
    /// <returns>The system DPI value.</returns>
    public static double GetSystemDpi()
    {
        return DefaultDpi;
    }
}
