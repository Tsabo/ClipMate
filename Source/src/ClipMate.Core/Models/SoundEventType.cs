namespace ClipMate.Core.Models;

/// <summary>
/// Defines the types of events that can trigger sound playback.
/// </summary>
public enum SoundEventType
{
    /// <summary>
    /// Triggered when a new clip is captured.
    /// </summary>
    ClipCaptured = 0,

    /// <summary>
    /// Triggered when a clip is pasted.
    /// </summary>
    ClipPasted = 1,

    /// <summary>
    /// Triggered when a clip is deleted.
    /// </summary>
    ClipDeleted = 2,

    /// <summary>
    /// Triggered when a search returns results.
    /// </summary>
    SearchSuccess = 3,

    /// <summary>
    /// Triggered when a search returns no results.
    /// </summary>
    SearchNoResults = 4,

    /// <summary>
    /// Triggered when a hotkey is pressed.
    /// </summary>
    HotkeyPressed = 5
}
