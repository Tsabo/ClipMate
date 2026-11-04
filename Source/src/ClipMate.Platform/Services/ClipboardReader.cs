using Windows.Win32.Foundation;
using ClipMate.Platform.Interop;

namespace ClipMate.Platform.Services;

/// <summary>
/// Who is reading the clipboard during a WM_RENDERFORMAT: the process of the window holding it open, and the process
/// in the foreground. Process ID 0 means no window.
/// </summary>
internal sealed record ClipboardReader(uint ProcessId, string ProcessName, uint ForegroundProcessId, string ForegroundProcessName)
{
    private const string _storeAppFrameHost = "ApplicationFrameHost";

    /// <summary>
    /// The reader as seen now, while the clipboard is open to <paramref name="openClipboardWindow" />.
    /// </summary>
    public static ClipboardReader Current(IWin32InputInterop win32, HWND openClipboardWindow)
    {
        var processId = GetProcessId(win32, openClipboardWindow);
        var foregroundProcessId = GetProcessId(win32, win32.GetForegroundWindow());

        return new ClipboardReader(processId, GetProcessName(win32, processId), foregroundProcessId, GetProcessName(win32, foregroundProcessId));
    }

    /// <summary>
    /// True when the window holding the clipboard open belongs to the application in the foreground, and that
    /// application is not ClipMate. A window-less caller's read cannot be told apart this way.
    /// </summary>
    public bool IsForegroundApplication(uint ownProcessId) =>
        ProcessId != 0 && ProcessId == ForegroundProcessId && ProcessId != ownProcessId;

    /// <summary>
    /// True when the read cannot be tied to a process: it came without a window, or a Store app is in the foreground. Its
    /// top-level window belongs to the frame host, and its clipboard reads come from a Windows service.
    /// </summary>
    public bool MayBeForForegroundApplication =>
        ProcessId == 0 || string.Equals(ForegroundProcessName, _storeAppFrameHost, StringComparison.OrdinalIgnoreCase);

    private static uint GetProcessId(IWin32InputInterop win32, HWND window)
    {
        if (window.IsNull)
            return 0;

        win32.GetWindowThreadProcessId(window, out var processId);

        return processId;
    }

    private static string GetProcessName(IWin32InputInterop win32, uint processId) =>
        processId == 0
            ? "(no window)"
            : win32.GetProcessName(processId) ?? "(unknown)";
}
