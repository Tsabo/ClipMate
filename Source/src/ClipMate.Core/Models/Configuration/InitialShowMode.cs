namespace ClipMate.Core.Models.Configuration;

/// <summary>
/// Defines which view to show initially when ClipMate starts.
/// </summary>
public enum InitialShowMode
{
    /// <summary>
    /// Start minimized to system tray (no window shown).
    /// </summary>
    Nothing,

    /// <summary>
    /// Show the Classic view window.
    /// </summary>
    Classic,

    /// <summary>
    /// Show the Explorer view window.
    /// </summary>
    Explorer
}
