using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Manages multiple database connections based on configuration.
/// Implements ClipMate 7.5 multi-database architecture.
/// </summary>
public class DatabaseManager : IDisposable
{
    private readonly IConfigurationService _configService;
    private readonly IDatabaseContextFactory _contextFactory;
    private readonly ILogger<DatabaseManager> _logger;
    private ClipMateConfiguration? _configuration;
    private bool _disposed;

    public DatabaseManager(IConfigurationService configService,
        IDatabaseContextFactory contextFactory,
        ILogger<DatabaseManager> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _contextFactory.Dispose();
        _disposed = true;
    }

    /// <summary>
    /// Loads all databases marked for auto-load from configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of databases successfully loaded.</returns>
    public async Task<int> LoadAutoLoadDatabasesAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _configuration = await _configService.LoadAsync(cancellationToken);
        var loadedCount = 0;

        foreach (var item in _configuration.Databases.Where(p => p.Value.AutoLoad))
        {
            var dbConfig = item.Value;
            try
            {
                _logger.LogInformation("Auto-loading database: {Name} at {Path}", dbConfig.Name, dbConfig.Directory);

                var context = _contextFactory.GetOrCreateContext(dbConfig.Directory);

                // Ensure database exists and is migrated
                await context.Database.EnsureCreatedAsync(cancellationToken);

                loadedCount++;
                _logger.LogInformation("Successfully loaded database: {Title}", dbConfig.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load database: {Title} at {Path}", dbConfig.Name, dbConfig.Directory);
            }
        }

        _logger.LogInformation("Loaded {Count} of {Total} auto-load databases",
            loadedCount, _configuration.Databases.Count(p => p.Value.AutoLoad));

        return loadedCount;
    }

    /// <summary>
    /// Loads a specific database by name.
    /// </summary>
    /// <param name="databaseName">Name of the database to load.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successfully loaded.</returns>
    public async Task<bool> LoadDatabaseAsync(string databaseName, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _configuration ??= await _configService.LoadAsync(cancellationToken);

        var dbEntry = _configuration.Databases.FirstOrDefault(p =>
            p.Value.Name.Equals(databaseName, StringComparison.OrdinalIgnoreCase));

        if (dbEntry.Key == null)
        {
            _logger.LogWarning("Database not found in configuration: {Name}", databaseName);

            return false;
        }

        var dbConfig = dbEntry.Value;

        try
        {
            _logger.LogInformation("Loading database: {Name} at {Path}", dbConfig.Name, dbConfig.Directory);

            var context = _contextFactory.GetOrCreateContext(dbConfig.Directory);
            await context.Database.EnsureCreatedAsync(cancellationToken);

            _logger.LogInformation("Successfully loaded database: {Title}", dbConfig.Name);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load database: {Title} at {Path}", dbConfig.Name, dbConfig.Directory);

            return false;
        }
    }

    /// <summary>
    /// Unloads a specific database.
    /// </summary>
    /// <param name="databaseTitle">Title of the database to unload.</param>
    /// <returns>True if successfully unloaded.</returns>
    public bool UnloadDatabase(string databaseName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_configuration == null)
            return false;

        var dbEntry = _configuration.Databases
            .FirstOrDefault(p => p.Value.Name.Equals(databaseName, StringComparison.OrdinalIgnoreCase));

        return dbEntry.Key != null && _contextFactory.CloseDatabase(dbEntry.Value.Directory);
    }

    /// <summary>
    /// Gets all loaded database configurations.
    /// </summary>
    /// <returns>Collection of loaded database configurations.</returns>
    public IEnumerable<DatabaseConfiguration> GetLoadedDatabases()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_configuration == null)
            return [];

        var loadedPaths = _contextFactory.GetLoadedDatabasePaths();

        return _configuration.Databases
            .Where(p => loadedPaths.Contains(p.Value.Directory, StringComparer.OrdinalIgnoreCase))
            .Select(p => p.Value);
    }

    /// <summary>
    /// Gets the database context for a specific database name.
    /// </summary>
    /// <param name="databaseName">Name of the database.</param>
    /// <returns>The database context, or null if not loaded.</returns>
    public ClipMateDbContext? GetDatabaseContext(string databaseName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_configuration == null)
            return null;

        var dbEntry = _configuration.Databases
            .FirstOrDefault(p => p.Value.Name.Equals(databaseName, StringComparison.OrdinalIgnoreCase));

        if (dbEntry.Key == null)
            return null;

        var dbConfig = dbEntry.Value;
        var loadedPaths = _contextFactory.GetLoadedDatabasePaths();

        if (!loadedPaths.Contains(dbConfig.Directory, StringComparer.OrdinalIgnoreCase))
            return null;

        return _contextFactory.GetOrCreateContext(dbConfig.Directory);
    }

    /// <summary>
    /// Gets all database contexts currently loaded.
    /// </summary>
    /// <returns>Collection of all loaded contexts with their names.</returns>
    public IEnumerable<(string Name, ClipMateDbContext Context)> GetAllDatabaseContexts()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_configuration == null)
            yield break;

        var loadedPaths = _contextFactory.GetLoadedDatabasePaths();

        foreach (var item in _configuration.Databases.Where(p =>
                     loadedPaths.Contains(p.Value.Directory, StringComparer.OrdinalIgnoreCase)))
        {
            var dbConfig = item.Value;
            var context = _contextFactory.GetOrCreateContext(dbConfig.Directory);

            yield return (dbConfig.Name, context);
        }
    }
}
