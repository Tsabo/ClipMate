using System.Windows;

namespace ClipMate.Platform;

/// <summary>
/// Interface for managing global hotkey registration and handling.
/// </summary>
public interface IHotkeyManager : IDisposable
{
    /// <summary>
    /// Initializes the hotkey manager with a window.
    /// </summary>
    /// <param name="window">The WPF window to use for receiving hotkey messages.</param>
    void Initialize(Window window);

    /// <summary>
    /// Registers a global hotkey.
    /// </summary>
    /// <param name="modifiers">The modifier keys (Ctrl, Alt, Shift, Win).</param>
    /// <param name="key">The virtual key code.</param>
    /// <param name="action">The action to invoke when the hotkey is pressed.</param>
    /// <returns>A unique ID for the registered hotkey, or -1 if registration failed.</returns>
    int RegisterHotkey(Core.Models.ModifierKeys modifiers, int key, Action action);

    /// <summary>
    /// Unregisters a hotkey by its ID.
    /// </summary>
    /// <param name="hotkeyId">The ID of the hotkey to unregister.</param>
    /// <returns>True if the hotkey was successfully unregistered, false otherwise.</returns>
    bool UnregisterHotkey(int hotkeyId);

    /// <summary>
    /// Unregisters all hotkeys.
    /// </summary>
    void UnregisterAll();
}
