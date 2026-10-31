using ClipMate.Core.Events;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services.Abstractions;
using CommunityToolkit.Mvvm.Messaging;
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
    private readonly IMessenger _messenger;
    private readonly DatabaseSchemaMigrationService _migrationService;
    private ClipMateConfiguration? _configuration;
    private bool _disposed;

    public DatabaseManager(IConfigurationService configService,
        IDatabaseContextFactory contextFactory,
        ILogger<DatabaseManager> logger,
        IMessenger messenger,
        DatabaseSchemaMigrationService migrationService)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _migrationService = migrationService ?? throw new ArgumentNullException(nameof(migrationService));
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

                await using var context = _contextFactory.CreateContext(dbConfig.FilePath);

                await OpenDatabaseAsync(context, cancellationToken);

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

        // Pick up databases added to the live configuration since it was cached (e.g. via Options)
        if (!_configuration.Databases.ContainsKey(databaseKey))
            _configuration = _configService.Configuration;

        if (!_configuration.Databases.TryGetValue(databaseKey, out var dbConfig))
        {
            _logger.LogWarning("Database not found in configuration: {Key}", databaseKey);

            return false;
        }

        try
        {
            _logger.LogInformation("Loading database: {Name} at {Path}", dbConfig.Name, dbConfig.FilePath);

            await using var context = _contextFactory.CreateContext(dbConfig.FilePath);
            await OpenDatabaseAsync(context, cancellationToken);

            _logger.LogInformation("Successfully loaded database: {Title}", dbConfig.Name);

            _messenger.Send(new StateRefreshRequestedEvent());
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load database: {Title} at {Path}", dbConfig.Name, dbConfig.FilePath);

            return false;
        }
    }

    /// <summary>
    /// Creates the database file if needed and migrates its schema, which also assigns a Database ID to databases
    /// created before the Database ID existed.
    /// </summary>
    private async Task OpenDatabaseAsync(ClipMateDbContext context, CancellationToken cancellationToken)
    {
        await context.Database.EnsureCreatedAsync(cancellationToken);
        await _migrationService.MigrateAsync(context, cancellationToken);
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

        var result = _configuration.Databases.TryGetValue(databaseKey, out var dbConfig) && _contextFactory.CloseDatabase(dbConfig.FilePath);
        if (result)
            _messenger.Send(new StateRefreshRequestedEvent());

        return result;
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
    /// Creates a new database context for a specific database key.
    /// The caller is responsible for disposing the returned context.
    /// </summary>
    /// <param name="databaseKey">Configuration key of the database.</param>
    /// <returns>A new database context, or null if the database is not loaded.</returns>
    public virtual ClipMateDbContext? CreateDatabaseContext(string databaseKey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_configuration == null)
            return null;

        if (!_configuration.Databases.TryGetValue(databaseKey, out var dbConfig))
            return null;

        var loadedPaths = _contextFactory.GetLoadedDatabasePaths();

        return !loadedPaths.Contains(dbConfig.FilePath, StringComparer.OrdinalIgnoreCase)
            ? null
            : _contextFactory.CreateContext(dbConfig.FilePath);
    }

    /// <summary>
    /// Creates database contexts for all currently loaded databases.
    /// The caller is responsible for disposing each returned context.
    /// </summary>
    /// <returns>Collection of new contexts with their database keys. Each context must be disposed by caller.</returns>
    public IEnumerable<(string DatabaseKey, ClipMateDbContext Context)> CreateAllDatabaseContexts()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_configuration == null)
            yield break;

        var loadedPaths = _contextFactory.GetLoadedDatabasePaths();

        foreach (var (databaseKey, dbConfig) in _configuration.Databases.Where(p =>
                     loadedPaths.Contains(p.Value.FilePath, StringComparer.OrdinalIgnoreCase)))
        {
            var context = _contextFactory.CreateContext(dbConfig.FilePath);

            yield return (databaseKey, context);
        }
    }
}
