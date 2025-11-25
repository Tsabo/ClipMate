using ClipMate.Core.Models.Configuration;

namespace ClipMate.Core.Services;

/// <summary>
/// Service for managing application configuration.
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// Gets the current configuration.
    /// </summary>
    ClipMateConfiguration Configuration { get; }

    /// <summary>
    /// Gets the path to the configuration file.
    /// </summary>
    string ConfigurationFilePath { get; }

    /// <summary>
    /// Loads configuration from disk. Creates default configuration if file doesn't exist.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded configuration.</returns>
    Task<ClipMateConfiguration> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the current configuration to disk.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the provided configuration to disk and updates current configuration.
    /// </summary>
    /// <param name="configuration">The configuration to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(ClipMateConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets configuration to defaults and saves to disk.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ResetToDefaultsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds or updates a database configuration.
    /// </summary>
    /// <param name="databaseId">Unique identifier for the database.</param>
    /// <param name="database">Database configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddOrUpdateDatabaseAsync(string databaseId, DatabaseConfiguration database, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a database configuration.
    /// </summary>
    /// <param name="databaseId">Unique identifier for the database to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveDatabaseAsync(string databaseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds or updates an application profile.
    /// </summary>
    /// <param name="applicationName">Application name (e.g., "DEVENV", "CHROME").</param>
    /// <param name="profile">Application profile configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddOrUpdateApplicationProfileAsync(string applicationName, ApplicationProfile profile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an application profile.
    /// </summary>
    /// <param name="applicationName">Application name to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveApplicationProfileAsync(string applicationName, CancellationToken cancellationToken = default);
}
