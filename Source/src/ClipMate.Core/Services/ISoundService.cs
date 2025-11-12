using ClipMate.Core.Models;

namespace ClipMate.Core.Services;

/// <summary>
/// Service for playing sounds in response to application events.
/// </summary>
public interface ISoundService
{
    /// <summary>
    /// Plays a sound for a specific event type.
    /// </summary>
    /// <param name="eventType">The event type that triggered the sound.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PlaySoundAsync(SoundEventType eventType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Configures a sound event.
    /// </summary>
    /// <param name="eventType">The event type.</param>
    /// <param name="soundFilePath">Path to the sound file.</param>
    /// <param name="volume">Volume level (0.0 to 1.0).</param>
    /// <param name="isEnabled">Whether the sound is enabled.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ConfigureSoundEventAsync(SoundEventType eventType, string soundFilePath, float volume = 1.0f, bool isEnabled = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all configured sound events.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of sound event configurations.</returns>
    Task<IReadOnlyList<SoundEvent>> GetAllSoundEventsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables or disables a sound event.
    /// </summary>
    /// <param name="eventType">The event type.</param>
    /// <param name="isEnabled">Whether to enable the sound.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetSoundEnabledAsync(SoundEventType eventType, bool isEnabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether sounds are globally enabled.
    /// </summary>
    bool IsSoundEnabled { get; }

    /// <summary>
    /// Sets whether sounds are globally enabled.
    /// </summary>
    void SetGlobalSoundEnabled(bool isEnabled);
}
