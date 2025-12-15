using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace ClipMate.Platform.Services;

/// <summary>
/// Windows implementation of idle time detection using Win32 APIs.
/// Uses GetLastInputInfo and GetTickCount64 to calculate user idle time.
/// </summary>
public class Win32IdleDetector : IWin32IdleDetector
{
    /// <inheritdoc />
    public uint GetIdleTimeMilliseconds()
    {
        var lastInputInfo = new LASTINPUTINFO
        {
            cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>(),
        };

        if (!PInvoke.GetLastInputInfo(ref lastInputInfo))
            return 0;

        var tickCount = PInvoke.GetTickCount64();
        var idleTime = tickCount - lastInputInfo.dwTime;

        return (uint)idleTime;
    }

    /// <inheritdoc />
    public bool IsIdle(TimeSpan idleThreshold)
    {
        var idleMs = GetIdleTimeMilliseconds();
        return idleMs >= idleThreshold.TotalMilliseconds;
    }
}
