using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Interop;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.HiDpi;

// Alias to resolve WPF vs WinForms ambiguity
using WpfApplication = System.Windows.Application;

namespace ClipMate.Platform;

/// <summary>
/// Helper class for DPI awareness and scaling calculations.
/// Uses CsWin32-generated P/Invoke methods for DPI management.
/// </summary>
public static class DpiHelper
{
    private const double _defaultDpi = 96.0;
    private static bool _isPerMonitorDpiAware;

    /// <summary>
    /// Initializes DPI awareness for the application.
    /// Supports Windows 8.1+ per-monitor DPI awareness with fallback to system DPI awareness.
    /// </summary>
    [SupportedOSPlatform("windows8.1")]
    public static void InitializeDpiAwareness()
    {
        try
        {
            // Try to set per-monitor DPI awareness (Windows 8.1+)
            if (OperatingSystem.IsWindowsVersionAtLeast(8, 1))
            {
                var result = PInvoke.SetProcessDpiAwareness(PROCESS_DPI_AWARENESS.PROCESS_PER_MONITOR_DPI_AWARE);
                _isPerMonitorDpiAware = result.Succeeded;
            }
            else
            {
                // Fall back to system DPI awareness (Windows Vista+)
                PInvoke.SetProcessDPIAware();
                _isPerMonitorDpiAware = false;
            }
        }
        catch
        {
            // Fall back to system DPI awareness if per-monitor fails
            try
            {
                PInvoke.SetProcessDPIAware();
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
    [SupportedOSPlatform("windows10.0.14393")]
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
            return (int)_defaultDpi;
        }

        try
        {
            // GetDpiForWindow requires Windows 10 Anniversary Update (14393) or later
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 14393))
            {
                var dpi = PInvoke.GetDpiForWindow(new HWND(hwnd));
                return dpi > 0 ? (int)dpi : (int)_defaultDpi;
            }
            else
            {
                return (int)_defaultDpi;
            }
        }
        catch
        {
            return (int)_defaultDpi;
        }
    }

    /// <summary>
    /// Gets the DPI scale factor for a window (1.0 = 96 DPI, 1.5 = 144 DPI, etc.).
    /// </summary>
    /// <param name="window">The window to get scale factor for.</param>
    /// <returns>The scale factor.</returns>
    [SupportedOSPlatform("windows10.0.14393")]
    public static double GetScaleFactor(Window window)
    {
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 14393))
        {
            var dpi = GetDpiForWindow(window);
            return dpi / _defaultDpi;
        }
        else
        {
            return 1.0; // Default scale factor on older Windows versions
        }
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
        var physicalWidth = PInvoke.GetSystemMetrics(Windows.Win32.UI.WindowsAndMessaging.SYSTEM_METRICS_INDEX.SM_CXSCREEN);
        var source = PresentationSource.FromVisual(WpfApplication.Current?.MainWindow);
        var scaleFactor = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
        return physicalWidth * scaleFactor;
    }

    /// <summary>
    /// Gets the primary screen height in device-independent pixels.
    /// </summary>
    /// <returns>The screen height.</returns>
    public static double GetScreenHeight()
    {
        var physicalHeight = PInvoke.GetSystemMetrics(Windows.Win32.UI.WindowsAndMessaging.SYSTEM_METRICS_INDEX.SM_CYSCREEN);
        var source = PresentationSource.FromVisual(WpfApplication.Current?.MainWindow);
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
        return _defaultDpi;
    }
}
