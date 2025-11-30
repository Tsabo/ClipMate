using ClipMate.Core.Models;

namespace ClipMate.Core.Services;

/// <summary>
/// Interface for storing and retrieving application profiles.
/// </summary>
public interface IApplicationProfileStore
{
    /// <summary>
    /// Loads all application profiles from storage.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of application profiles keyed by application name.</returns>
    Task<Dictionary<string, ApplicationProfile>> LoadProfilesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves all application profiles to storage.
    /// </summary>
    /// <param name="profiles">Dictionary of application profiles to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveProfilesAsync(Dictionary<string, ApplicationProfile> profiles, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific application profile by name.
    /// </summary>
    /// <param name="applicationName">The application name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The application profile or null if not found.</returns>
    Task<ApplicationProfile?> GetProfileAsync(string applicationName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds or updates an application profile.
    /// </summary>
    /// <param name="profile">The profile to add or update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddOrUpdateProfileAsync(ApplicationProfile profile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a specific application profile.
    /// </summary>
    /// <param name="applicationName">The application name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteProfileAsync(string applicationName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all application profiles.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAllProfilesAsync(CancellationToken cancellationToken = default);
}
