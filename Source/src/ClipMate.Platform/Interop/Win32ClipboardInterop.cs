using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

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
    public unsafe string? GetWindowTitle(HWND hwnd)
    {
        var length = PInvoke.GetWindowTextLength(hwnd);
        if (length <= 0)
            return null;

        var buffer = new char[length + 1];
        fixed (char* pBuffer = buffer)
        {
            var copied = PInvoke.GetWindowText(hwnd, pBuffer, buffer.Length);

            return copied > 0 ? new string(buffer, 0, copied) : null;
        }
    }

    /// <inheritdoc/>
    public HWND GetTopLevelWindow(HWND hwnd) => PInvoke.GetAncestor(hwnd, GET_ANCESTOR_FLAGS.GA_ROOT);

    /// <inheritdoc/>
    public unsafe uint GetWindowProcessId(HWND hwnd)
    {
        uint processId = 0;
        _ = PInvoke.GetWindowThreadProcessId(hwnd, &processId);

        return processId;
    }

    /// <inheritdoc/>
    public string? GetProcessName(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);

            return process.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            // The process has exited
            return null;
        }
    }

    /// <inheritdoc/>
    public HWND GetClipboardOwner() => PInvoke.GetClipboardOwner();

    /// <inheritdoc/>
    public bool OpenClipboard(HWND hwnd) => PInvoke.OpenClipboard(hwnd);

    /// <inheritdoc />
    public uint GetClipboardSequenceNumber() => PInvoke.GetClipboardSequenceNumber();

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
