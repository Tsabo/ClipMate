using ClipMate.Core.Models;

namespace ClipMate.Core.Services;

/// <summary>
/// Service for managing application-specific clipboard format capture profiles.
/// Provides format filtering by profile; existing profiles learn the formats they haven't seen.
/// </summary>
public interface IApplicationProfileService
{
    /// <summary>
    /// Gets whether application profiles feature is currently enabled.
    /// This is a session-only setting controlled from the Options dialog.
    /// </summary>
    /// <returns>True if application profiles are enabled, false otherwise.</returns>
    bool IsApplicationProfilesEnabled();

    /// <summary>
    /// Sets whether application profiles feature is enabled for the current session.
    /// This setting is not persisted and resets to false on application restart.
    /// </summary>
    /// <param name="enabled">True to enable application profiles, false to disable.</param>
    void SetApplicationProfilesEnabled(bool enabled);

    /// <summary>
    /// The formats of a capture the Source Application's profile keeps, or null when its profile is switched off.
    /// As in ClipMate 7, an application without a profile gets one (a preset for programs ClipMate 7 knows, else HTML
    /// and text, or the image when there is no text). Formats the profile doesn't have yet are added (text and RTF on,
    /// others off) and the profile is saved. With profiles switched off every format is kept.
    /// </summary>
    /// <param name="applicationName">The Source Application's name; it is normalized.</param>
    /// <param name="formatNames">The names of the formats on the clipboard, as the profile stores them.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlySet<string>?> ApplyProfileAsync(string applicationName, IReadOnlyCollection<string> formatNames,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing application profile.
    /// </summary>
    /// <param name="profile">The profile to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateProfileAsync(ApplicationProfile profile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all application profiles.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of profiles keyed by application name.</returns>
    Task<Dictionary<string, ApplicationProfile>> GetAllProfilesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an application profile by name.
    /// </summary>
    /// <param name="applicationName">The application name to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteProfileAsync(string applicationName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all application profiles.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAllProfilesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Normalizes an application name by removing .EXE extension and converting to uppercase.
    /// </summary>
    /// <param name="applicationName">The application name to normalize.</param>
    /// <returns>Normalized application name.</returns>
    string NormalizeApplicationName(string applicationName);
}
