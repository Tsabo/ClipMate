namespace ClipMate.Core.Models;

/// <summary>
/// Modifier keys for hotkey combinations (flags enum for combinations).
/// </summary>
[Flags]
public enum ModifierKeys
{
    /// <summary>
    /// No modifier keys.
    /// </summary>
    None = 0,

    /// <summary>
    /// Alt key modifier.
    /// </summary>
    Alt = 1 << 0,

    /// <summary>
    /// Ctrl (Control) key modifier.
    /// </summary>
    Control = 1 << 1,

    /// <summary>
    /// Shift key modifier.
    /// </summary>
    Shift = 1 << 2,

    /// <summary>
    /// Windows key modifier.
    /// </summary>
    Windows = 1 << 3
}
