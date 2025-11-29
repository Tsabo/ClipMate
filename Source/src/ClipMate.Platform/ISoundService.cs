namespace ClipMate.Platform;

/// <summary>
/// Represents different types of sound events in the application.
/// </summary>
public enum SoundEvent
{
    /// <summary>
    /// Sound played when the clipboard is updated with new content.
    /// </summary>
    ClipboardUpdate,

    /// <summary>
    /// Sound played when clips are appended together.
    /// </summary>
    Append,

    /// <summary>
    /// Sound played when clips are erased.
    /// </summary>
    Erase,

    /// <summary>
    /// Sound played when clips are filtered.
    /// </summary>
    Filter,

    /// <summary>
    /// Sound played when clipboard content is ignored.
    /// </summary>
    Ignore
}

/// <summary>
/// Defines a service for playing application sound notifications.
/// </summary>
public interface ISoundService
{
    /// <summary>
    /// Plays a sound asynchronously for the specified event type.
    /// </summary>
    /// <param name="soundEvent">The type of sound event to play.</param>
    /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// The sound will only play if:
    /// 1. A sound file exists for the event type (e.g., "clipboard-update.wav" in Assets/Sounds/)
    /// 2. The corresponding preference flag is enabled (e.g., BeepOnUpdate for ClipboardUpdate)
    /// If no sound file exists or the preference is disabled, this method completes silently.
    /// </remarks>
    Task PlaySoundAsync(SoundEvent soundEvent, CancellationToken cancellationToken = default);
}
