namespace ClipMate.Core.Models.Configuration;

/// <summary>
/// Defines the action to perform when clicking on taskbar or system tray icons.
/// </summary>
public enum IconLeftClickAction
{
    /// <summary>
    /// Show the main window (Explorer).
    /// </summary>
    ShowExplorerWindow,

    /// <summary>
    /// Show the ClipBar popup.
    /// </summary>
    ShowClipBar,
}
