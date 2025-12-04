using ClipMate.Core.Models;

namespace ClipMate.Platform;

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
