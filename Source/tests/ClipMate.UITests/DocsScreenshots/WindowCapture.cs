using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using ClipMate.UITests.Fixtures;

namespace ClipMate.UITests.DocsScreenshots;

/// <summary>
/// Saves one window's own pixels: PrintWindow renders the window whatever lies on top of it, so another program's
/// window can't end up in a picture. Works in physical pixels, which needs <see cref="UsePhysicalPixels" />.
/// </summary>
internal static partial class WindowCapture
{
    private const uint _printWindowRenderFullContent = 2;
    private static readonly nint _perMonitorAwareV2 = -4;
    private static readonly nint _perMonitorAware = 2;

    /// <summary>
    /// Makes this process per-monitor DPI aware, so UI Automation, the mouse and captures all use physical pixels. Fixed
    /// for the process once a window exists, which is why it is the first thing a run does.
    /// </summary>
    public static void UsePhysicalPixels()
    {
        SetProcessDpiAwarenessContext(_perMonitorAwareV2);
        if (GetAwarenessFromDpiAwarenessContext(GetThreadDpiAwarenessContext()) != _perMonitorAware)
            throw new InvalidOperationException("Could not make the test process per-monitor DPI aware");
    }

    /// <summary>
    /// The window's DPI, for the run's report.
    /// </summary>
    public static uint DpiOf(nint window) => GetDpiForWindow(window);

    /// <summary>
    /// Captures <paramref name="window" /> until <paramref name="matches" /> captures in a row are the same, so it has
    /// finished painting, and saves the result as a PNG.
    /// </summary>
    /// <returns>The picture's size in pixels.</returns>
    public static async Task<Size> SaveWhenSettledAsync(nint window, string path, int matches = 2)
    {
        byte[]? previous = null;
        var same = 0;
        await Poll.UntilAsync(() =>
        {
            var current = Pixels(window);
            same = previous != null && current.AsSpan().SequenceEqual(previous) ? same + 1 : 1;
            previous = current;
            return same >= matches;
        }, $"{Path.GetFileName(path)} to stop changing", TimeSpan.FromSeconds(30));

        using var bitmap = Capture(window);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        return bitmap.Size;
    }

    private static byte[] Pixels(nint window)
    {
        using var bitmap = Capture(window);
        var data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadOnly, bitmap.PixelFormat);
        try
        {
            var pixels = new byte[Math.Abs(data.Stride) * data.Height];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            return pixels;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static Bitmap Capture(nint window)
    {
        var bounds = AutomationElement.FromHandle(window).Current.BoundingRectangle;
        var bitmap = new Bitmap((int)bounds.Width, (int)bounds.Height, PixelFormat.Format32bppRgb);
        using var graphics = Graphics.FromImage(bitmap);
        var deviceContext = graphics.GetHdc();
        try
        {
            if (!PrintWindow(window, deviceContext, _printWindowRenderFullContent))
                throw new InvalidOperationException("PrintWindow failed");
        }
        finally
        {
            graphics.ReleaseHdc(deviceContext);
        }

        return bitmap;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PrintWindow(nint window, nint deviceContext, uint flags);

    [LibraryImport("user32.dll")]
    private static partial uint GetDpiForWindow(nint window);

    [LibraryImport("user32.dll")]
    private static partial nint SetProcessDpiAwarenessContext(nint context);

    [LibraryImport("user32.dll")]
    private static partial nint GetThreadDpiAwarenessContext();

    [LibraryImport("user32.dll")]
    private static partial nint GetAwarenessFromDpiAwarenessContext(nint context);
}
