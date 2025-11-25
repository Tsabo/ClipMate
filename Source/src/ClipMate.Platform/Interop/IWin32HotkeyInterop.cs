using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace ClipMate.Platform.Interop;

/// <summary>
/// Abstraction for Win32 hotkey API calls to enable testing.
/// </summary>
public interface IWin32HotkeyInterop
{
    /// <summary>
    /// Registers a system-wide hotkey.
    /// </summary>
    bool RegisterHotKey(HWND hWnd, int id, HOT_KEY_MODIFIERS fsModifiers, uint vk);

    /// <summary>
    /// Unregisters a system-wide hotkey.
    /// </summary>
    bool UnregisterHotKey(HWND hWnd, int id);
}
