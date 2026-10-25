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
    /// The window's title, or null when it has none.
    /// </summary>
    string? GetWindowTitle(HWND hwnd);

    /// <summary>
    /// The top-level window that contains <paramref name="hwnd" /> (the window itself when it is top-level).
    /// </summary>
    HWND GetTopLevelWindow(HWND hwnd);

    /// <summary>
    /// The ID of the process that created the window, or 0 when the window doesn't exist.
    /// </summary>
    uint GetWindowProcessId(HWND hwnd);

    /// <summary>
    /// The name of a running process (without ".exe"), or null when it has exited.
    /// </summary>
    string? GetProcessName(uint processId);

    /// <summary>
    /// The window that owns the clipboard's content, or null when there is none.
    /// </summary>
    HWND GetClipboardOwner();

    /// <summary>
    /// Opens the clipboard for reading, associated with the window.
    /// </summary>
    bool OpenClipboard(HWND hwnd);

    /// <summary>
    /// A number that changes every time the clipboard's content changes.
    /// </summary>
    uint GetClipboardSequenceNumber();

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
