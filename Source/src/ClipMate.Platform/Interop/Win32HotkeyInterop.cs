using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace ClipMate.Platform.Interop;

/// <summary>
/// Default implementation of <see cref="IWin32HotkeyInterop"/> that delegates to CsWin32 PInvoke methods.
/// </summary>
public class Win32HotkeyInterop : IWin32HotkeyInterop
{
    /// <inheritdoc/>
    public bool RegisterHotKey(HWND hWnd, int id, HOT_KEY_MODIFIERS fsModifiers, uint vk) =>
        PInvoke.RegisterHotKey(hWnd, id, fsModifiers, vk);

    /// <inheritdoc/>
    public bool UnregisterHotKey(HWND hWnd, int id) =>
        PInvoke.UnregisterHotKey(hWnd, id);
}
