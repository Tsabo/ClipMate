using Windows.Win32.Foundation;

namespace ClipMate.Platform.Interop;

/// <summary>
/// Abstraction for Win32 clipboard API calls to enable testing.
/// </summary>
public interface IWin32ClipboardInterop
{
    /// <summary>
    /// Registers a window to receive clipboard change notifications.
    /// </summary>
    bool AddClipboardFormatListener(HWND hwnd);

    /// <summary>
    /// Unregisters a window from receiving clipboard change notifications.
    /// </summary>
    bool RemoveClipboardFormatListener(HWND hwnd);

    /// <summary>
    /// Gets the handle of the foreground window.
    /// </summary>
    HWND GetForegroundWindow();

    /// <summary>
    /// Gets the length of the window text.
    /// </summary>
    int GetWindowTextLength(HWND hwnd);

    /// <summary>
    /// Gets the text of the specified window.
    /// </summary>
    unsafe int GetWindowText(HWND hwnd, char* lpString, int nMaxCount);
}
