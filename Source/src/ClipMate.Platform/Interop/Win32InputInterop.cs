using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace ClipMate.Platform.Interop;

/// <summary>
/// Default implementation of <see cref="IWin32InputInterop"/> that delegates to CsWin32 PInvoke methods.
/// </summary>
public class Win32InputInterop : IWin32InputInterop
{
    /// <inheritdoc/>
    public HWND GetForegroundWindow() => PInvoke.GetForegroundWindow();

    /// <inheritdoc/>
    public unsafe int GetWindowText(HWND hwnd, char* lpString, int nMaxCount) =>
        PInvoke.GetWindowText(hwnd, lpString, nMaxCount);

    /// <inheritdoc/>
    public uint GetWindowThreadProcessId(HWND hWnd, out uint lpdwProcessId) =>
        PInvoke.GetWindowThreadProcessId(hWnd, out lpdwProcessId);

    /// <inheritdoc/>
    public unsafe uint SendInput(uint cInputs, INPUT* pInputs, int cbSize) =>
        PInvoke.SendInput(cInputs, pInputs, cbSize);
}
