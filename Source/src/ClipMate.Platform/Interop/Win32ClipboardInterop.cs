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

    /// <inheritdoc/>
    public bool OpenClipboard(HWND hwnd) => PInvoke.OpenClipboard(hwnd);

    /// <inheritdoc/>
    public bool CloseClipboard() => PInvoke.CloseClipboard();

    /// <inheritdoc/>
    public uint EnumClipboardFormats(uint format) => PInvoke.EnumClipboardFormats(format);

    /// <inheritdoc/>
    public unsafe string? GetClipboardFormatName(uint format)
    {
        const int maxNameLength = 256;
        var buffer = stackalloc char[maxNameLength];
        var length = PInvoke.GetClipboardFormatName(format, buffer, maxNameLength);

        return length > 0 ? new string(buffer, 0, length) : null;
    }

    /// <inheritdoc/>
    public unsafe byte[]? GetClipboardData(uint format)
    {
        var handle = PInvoke.GetClipboardData(format);
        if (handle.IsNull)
            return null;

        var hMem = new HGLOBAL(handle.Value);
        var ptr = PInvoke.GlobalLock(hMem);
        if (ptr == null)
            return null;

        try
        {
            return new ReadOnlySpan<byte>(ptr, (int)PInvoke.GlobalSize(hMem)).ToArray();
        }
        finally
        {
            PInvoke.GlobalUnlock(hMem);
        }
    }
}
