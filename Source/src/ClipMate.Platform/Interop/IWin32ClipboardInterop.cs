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

    /// <summary>
    /// Opens the clipboard for reading, associated with the window.
    /// </summary>
    bool OpenClipboard(HWND hwnd);

    /// <summary>
    /// Closes the clipboard opened by <see cref="OpenClipboard" />.
    /// </summary>
    bool CloseClipboard();

    /// <summary>
    /// The format that follows <paramref name="format" /> on the open clipboard, in the order the source offered them
    /// (0 starts the list); 0 when there are no more.
    /// </summary>
    uint EnumClipboardFormats(uint format);

    /// <summary>
    /// The name of a registered clipboard format, or null for a format without one.
    /// </summary>
    string? GetClipboardFormatName(uint format);

    /// <summary>
    /// The content of a format's global memory block on the open clipboard, or null when the clipboard gives no data
    /// for it (e.g. the source failed to render it).
    /// </summary>
    byte[]? GetClipboardData(uint format);
}
