namespace ClipMate.Core.Models;

/// <summary>
/// Specifies the sound playback mode for sound events.
/// </summary>
public enum SoundMode
{
    /// <summary>
    /// No sound will be played for this event.
    /// </summary>
    Off = 0,

    /// <summary>
    /// The default sound file will be played from Assets/Sounds/ directory.
    /// </summary>
    Default = 1,

    /// <summary>
    /// A custom user-specified sound file will be played.
    /// </summary>
    Custom = 2
}
