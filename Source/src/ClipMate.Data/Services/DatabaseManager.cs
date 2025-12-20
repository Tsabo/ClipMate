using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Manages multiple database connections based on configuration.
/// Implements ClipMate 7.5 multi-database architecture.
/// </summary>
internal class DatabaseManager : IDatabaseManager
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
                _logger.LogInformation("Auto-loading database: {Name} at {Path}", dbConfig.Name, dbConfig.FilePath);

                var context = _contextFactory.GetOrCreateContext(dbConfig.FilePath);

                // Ensure database exists and is migrated
                await context.Database.EnsureCreatedAsync(cancellationToken);

                loadedCount++;
                _logger.LogInformation("Successfully loaded database: {Title}", dbConfig.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load database: {Title} at {Path}", dbConfig.Name, dbConfig.FilePath);
            }
        }

        _logger.LogInformation("Loaded {Count} of {Total} auto-load databases",
            loadedCount, _configuration.Databases.Count(p => p.Value.AutoLoad));

        return loadedCount;
    }

    /// <summary>
    /// Loads a specific database by its configuration key.
    /// </summary>
    /// <param name="databaseKey">Configuration key of the database to load.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successfully loaded.</returns>
    public async Task<bool> LoadDatabaseAsync(string databaseKey, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _configuration ??= await _configService.LoadAsync(cancellationToken);

        if (!_configuration.Databases.TryGetValue(databaseKey, out var dbConfig))
        {
            _logger.LogWarning("Database not found in configuration: {Key}", databaseKey);

            return false;
        }

        try
        {
            _logger.LogInformation("Loading database: {Name} at {Path}", dbConfig.Name, dbConfig.FilePath);

            var context = _contextFactory.GetOrCreateContext(dbConfig.FilePath);
            await context.Database.EnsureCreatedAsync(cancellationToken);

            _logger.LogInformation("Successfully loaded database: {Title}", dbConfig.Name);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load database: {Title} at {Path}", dbConfig.Name, dbConfig.FilePath);

            return false;
        }
    }

    /// <summary>
    /// Unloads a specific database.
    /// </summary>
    /// <param name="databaseKey">Configuration key of the database to unload.</param>
    /// <returns>True if successfully unloaded.</returns>
    public bool UnloadDatabase(string databaseKey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_configuration == null)
            return false;

        return _configuration.Databases.TryGetValue(databaseKey, out var dbConfig) && _contextFactory.CloseDatabase(dbConfig.FilePath);
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
            .Where(p => loadedPaths.Contains(p.Value.FilePath, StringComparer.OrdinalIgnoreCase))
            .Select(p => p.Value);
    }

    /// <summary>
    /// Gets the database context for a specific database key.
    /// </summary>
    /// <param name="databaseKey">Configuration key of the database.</param>
    /// <returns>The database context, or null if not loaded.</returns>
    public virtual ClipMateDbContext? GetDatabaseContext(string databaseKey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_configuration == null)
            return null;

        if (!_configuration.Databases.TryGetValue(databaseKey, out var dbConfig))
            return null;

        var loadedPaths = _contextFactory.GetLoadedDatabasePaths();

        return !loadedPaths.Contains(dbConfig.FilePath, StringComparer.OrdinalIgnoreCase)
            ? null
            : _contextFactory.GetOrCreateContext(dbConfig.FilePath);
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
                     loadedPaths.Contains(p.Value.FilePath, StringComparer.OrdinalIgnoreCase)))
        {
            var dbConfig = item.Value;
            var context = _contextFactory.GetOrCreateContext(dbConfig.FilePath);

            yield return (dbConfig.Name, context);
        }
    }
}
