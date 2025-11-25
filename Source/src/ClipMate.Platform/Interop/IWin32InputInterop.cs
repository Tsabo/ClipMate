using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace ClipMate.Platform.Interop;

/// <summary>
/// Abstraction for Win32 input and window API calls to enable testing.
/// </summary>
public interface IWin32InputInterop
{
    /// <summary>
    /// Gets the handle of the foreground window.
    /// </summary>
    HWND GetForegroundWindow();

    /// <summary>
    /// Gets the text of the specified window.
    /// </summary>
    unsafe int GetWindowText(HWND hwnd, char* lpString, int nMaxCount);

    /// <summary>
    /// Retrieves the identifier of the thread that created the specified window.
    /// </summary>
    uint GetWindowThreadProcessId(HWND hWnd, out uint lpdwProcessId);

    /// <summary>
    /// Sends input events to the system.
    /// </summary>
    unsafe uint SendInput(uint cInputs, INPUT* pInputs, int cbSize);
}
