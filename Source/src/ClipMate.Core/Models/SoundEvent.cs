namespace ClipMate.Core.Models;

/// <summary>
/// Represents a sound event configuration mapping event types to sound files.
/// </summary>
public class SoundEvent
{
    /// <summary>
    /// Unique identifier for the sound event.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The type of event that triggers this sound.
    /// </summary>
    public SoundEventType EventType { get; set; }

    /// <summary>
    /// Absolute file path to the sound file (.wav, .mp3, etc.).
    /// </summary>
    public string SoundFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Volume level (0.0 to 1.0).
    /// </summary>
    public float Volume { get; set; } = 1.0f;

    /// <summary>
    /// Whether this sound event is currently enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Timestamp when the sound event was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp of last modification.
    /// </summary>
    public DateTime? ModifiedAt { get; set; }
}
