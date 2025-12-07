namespace ClipMate.Core.Models.Configuration;

/// <summary>
/// Defines where the ClipBar popup window should appear.
/// </summary>
public enum ClipBarPopupLocation
{
    /// <summary>
    /// Remember and restore the last window position.
    /// </summary>
    RememberLastLocation,

    /// <summary>
    /// Position above the Windows taskbar on the active monitor.
    /// Falls back to mouse cursor if taskbar detection fails.
    /// </summary>
    AboveTaskbar,

    /// <summary>
    /// Position at the current mouse cursor location.
    /// </summary>
    AtMouseCursor,
}
