using ClipMate.Core.Models;

namespace ClipMate.Core.Services;

/// <summary>
/// Service for managing application-specific clipboard format capture profiles.
/// Provides auto-creation of profiles with smart defaults and format filtering.
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
    /// Determines if a specific clipboard format should be captured for a given application.
    /// Automatically creates a profile with smart defaults if the application is not yet profiled.
    /// </summary>
    /// <param name="applicationName">The application name (normalized, uppercase, without .EXE).</param>
    /// <param name="formatName">The clipboard format name to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the format should be captured, false otherwise.</returns>
    Task<bool> ShouldCaptureFormatAsync(string applicationName, string formatName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an existing application profile or creates a new one with smart defaults.
    /// </summary>
    /// <param name="applicationName">The application name (normalized, uppercase, without .EXE).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The application profile.</returns>
    Task<ApplicationProfile> GetOrCreateProfileAsync(string applicationName, CancellationToken cancellationToken = default);

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
