using ClipMate.Core.Models;

namespace ClipMate.Core.Services;

/// <summary>
/// Service for managing global hotkeys for clipboard operations.
/// </summary>
public interface IHotkeyService
{
    /// <summary>
    /// Registers a hotkey with the system.
    /// </summary>
    /// <param name="id">Unique identifier for the hotkey.</param>
    /// <param name="modifiers">Modifier keys (Ctrl, Alt, Shift, Win).</param>
    /// <param name="key">The primary key.</param>
    /// <param name="action">Action to execute when hotkey is pressed.</param>
    /// <returns>True if registered successfully; otherwise, false.</returns>
    bool RegisterHotkey(int id, ModifierKeys modifiers, int key, Action action);

    /// <summary>
    /// Unregisters a hotkey from the system.
    /// </summary>
    /// <param name="id">The hotkey identifier.</param>
    /// <returns>True if unregistered successfully; otherwise, false.</returns>
    bool UnregisterHotkey(int id);

    /// <summary>
    /// Unregisters all hotkeys.
    /// </summary>
    void UnregisterAllHotkeys();

    /// <summary>
    /// Gets whether a hotkey is currently registered.
    /// </summary>
    /// <param name="id">The hotkey identifier.</param>
    /// <returns>True if registered; otherwise, false.</returns>
    bool IsHotkeyRegistered(int id);
}
