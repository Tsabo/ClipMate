using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

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

    /// <summary>
    /// Retrieves the name of the class to which the specified window belongs.
    /// </summary>
    unsafe int GetClassName(HWND hWnd, char* lpClassName, int nMaxCount);

    /// <summary>
    /// Brings the specified window to the foreground and activates it.
    /// </summary>
    bool SetForegroundWindow(HWND hWnd);

    /// <summary>
    /// Retrieves a window that has the specified relationship (Z-order or owner) to the specified window.
    /// </summary>
    HWND GetWindow(HWND hWnd, GET_WINDOW_CMD uCmd);

    /// <summary>
    /// Determines whether the specified handle identifies an existing window.
    /// </summary>
    bool IsWindow(HWND hWnd);

    /// <summary>
    /// Determines whether the specified window has the WS_VISIBLE style.
    /// </summary>
    bool IsWindowVisible(HWND hWnd);

    /// <summary>
    /// Determines whether the Desktop Window Manager cloaks the specified window (for example a suspended Store app
    /// or a window on another virtual desktop), which leaves it "visible" but not shown.
    /// </summary>
    bool IsWindowCloaked(HWND hWnd);

    /// <summary>
    /// Gets the name of the process's main module without extension (e.g. "notepad"), or null when the process has exited.
    /// </summary>
    string? GetProcessName(uint processId);
}
