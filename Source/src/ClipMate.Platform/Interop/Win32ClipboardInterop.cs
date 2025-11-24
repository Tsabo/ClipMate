using Windows.Win32;
using Windows.Win32.Foundation;

namespace ClipMate.Platform.Interop;

/// <summary>
/// Default implementation of <see cref="IWin32ClipboardInterop"/> that delegates to CsWin32 PInvoke methods.
/// </summary>
public class Win32ClipboardInterop : IWin32ClipboardInterop
{
    /// <inheritdoc/>
    public bool AddClipboardFormatListener(HWND hwnd) => PInvoke.AddClipboardFormatListener(hwnd);

    /// <inheritdoc/>
    public bool RemoveClipboardFormatListener(HWND hwnd) => PInvoke.RemoveClipboardFormatListener(hwnd);

    /// <inheritdoc/>
    public HWND GetForegroundWindow() => PInvoke.GetForegroundWindow();

    /// <inheritdoc/>
    public int GetWindowTextLength(HWND hwnd) => PInvoke.GetWindowTextLength(hwnd);

    /// <inheritdoc/>
    public unsafe int GetWindowText(HWND hwnd, char* lpString, int nMaxCount) =>
        PInvoke.GetWindowText(hwnd, lpString, nMaxCount);
}
