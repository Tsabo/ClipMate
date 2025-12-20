using ClipMate.Core.Models.Configuration;

namespace ClipMate.Data.Services;

public interface IDatabaseManager : IDisposable
{
    /// <summary>
    /// Loads all databases marked for auto-load from configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of databases successfully loaded.</returns>
    Task<int> LoadAutoLoadDatabasesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a specific database by its configuration key.
    /// </summary>
    /// <param name="databaseKey">Configuration key of the database to load.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successfully loaded.</returns>
    Task<bool> LoadDatabaseAsync(string databaseKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unloads a specific database.
    /// </summary>
    /// <param name="databaseKey">Configuration key of the database to unload.</param>
    /// <returns>True if successfully unloaded.</returns>
    bool UnloadDatabase(string databaseKey);

    /// <summary>
    /// Gets all loaded database configurations.
    /// </summary>
    /// <returns>Collection of loaded database configurations.</returns>
    IEnumerable<DatabaseConfiguration> GetLoadedDatabases();

    /// <summary>
    /// Gets the database context for a specific database key.
    /// </summary>
    /// <param name="databaseKey">Configuration key of the database.</param>
    /// <returns>The database context, or null if not loaded.</returns>
    ClipMateDbContext? GetDatabaseContext(string databaseKey);

    /// <summary>
    /// Gets all database contexts currently loaded.
    /// </summary>
    /// <returns>Collection of all loaded contexts with their names.</returns>
    IEnumerable<(string Name, ClipMateDbContext Context)> GetAllDatabaseContexts();
}
