using ClipMate.Core.Models.Configuration;

namespace ClipMate.Data.Services;

public interface IDatabaseManager : IDisposable
{
    /// <summary>
    /// Loads all databases marked for auto-load from configuration.
    /// Ensures the database files exist and schemas are created.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of databases successfully loaded.</returns>
    Task<int> LoadAutoLoadDatabasesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a specific database by its configuration key.
    /// Ensures the database file exists and schema is created.
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
    /// Creates a new database context for a specific database key.
    /// The caller is responsible for disposing the returned context.
    /// </summary>
    /// <param name="databaseKey">Configuration key of the database.</param>
    /// <returns>A new database context, or null if the database is not loaded.</returns>
    ClipMateDbContext? CreateDatabaseContext(string databaseKey);

    /// <summary>
    /// Creates database contexts for all currently loaded databases.
    /// The caller is responsible for disposing each returned context.
    /// </summary>
    /// <returns>Collection of new contexts with their database keys. Each context must be disposed by caller.</returns>
    IEnumerable<(string DatabaseKey, ClipMateDbContext Context)> CreateAllDatabaseContexts();
}
