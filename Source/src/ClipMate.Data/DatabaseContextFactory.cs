using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using ClipMate.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data;

/// <summary>
/// Factory for creating ClipMateDbContext instances for multiple databases
/// and database-specific repository instances.
/// Supports the multi-database architecture from ClipMate 7.5.
/// DbContext instances are NOT cached - each call creates a fresh, short-lived context
/// (EF Core best practice for thread safety in concurrent applications).
/// </summary>
public class DatabaseContextFactory : IDatabaseContextFactory
{
    private readonly IConfigurationService _configurationService;
    private readonly HashSet<string> _loadedDatabases;
    private readonly Lock _lock = new();
    private readonly ILogger<DatabaseContextFactory> _logger;
    private readonly IServiceProvider _serviceProvider;
    private bool _disposed;

    public DatabaseContextFactory(IServiceProvider serviceProvider,
        IConfigurationService configurationService,
        ILogger<DatabaseContextFactory> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loadedDatabases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates a new database context for the specified database path.
    /// The context is NOT cached - caller is responsible for disposal.
    /// Also registers the database path as "loaded" if not already registered.
    /// </summary>
    /// <param name="databasePath">Full path to the SQLite database file.</param>
    /// <returns>A new ClipMateDbContext instance for the database.</returns>
    public ClipMateDbContext CreateContext(string databasePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("Database path cannot be null or empty.", nameof(databasePath));

        var normalizedPath = Path.GetFullPath(databasePath);

        lock (_lock)
        {
            _loadedDatabases.Add(normalizedPath);
        }

        _logger.LogDebug("Creating context for database: {DatabasePath}", normalizedPath);

        var optionsBuilder = new DbContextOptionsBuilder<ClipMateDbContext>();
        optionsBuilder.UseSqlite($"Data Source={normalizedPath}");

        return new ClipMateDbContext(optionsBuilder.Options);
    }

    /// <summary>
    /// Registers a database path as "loaded" without creating a context.
    /// Use this when you need to track that a database is available but don't need a context yet.
    /// </summary>
    /// <param name="databasePath">Full path to the SQLite database file.</param>
    public void RegisterDatabase(string databasePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("Database path cannot be null or empty.", nameof(databasePath));

        var normalizedPath = Path.GetFullPath(databasePath);

        lock (_lock)
        {
            if (_loadedDatabases.Add(normalizedPath))
                _logger.LogInformation("Registered database: {DatabasePath}", normalizedPath);
        }
    }

    /// <summary>
    /// Gets all database paths currently registered as loaded.
    /// </summary>
    /// <returns>Collection of database paths.</returns>
    public IReadOnlyCollection<string> GetLoadedDatabasePaths()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            return _loadedDatabases.ToList();
        }
    }

    /// <summary>
    /// Unregisters a database path, marking it as no longer loaded.
    /// </summary>
    /// <param name="databasePath">Path to the database to unregister.</param>
    /// <returns>True if the database was registered and has been unregistered.</returns>
    public bool CloseDatabase(string databasePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var normalizedPath = Path.GetFullPath(databasePath);

        lock (_lock)
        {
            if (!_loadedDatabases.Remove(normalizedPath))
                return false;
        }

        _logger.LogInformation("Unregistered database: {DatabasePath}", normalizedPath);

        return true;
    }

    /// <summary>
    /// Creates a ClipRepository instance for the specified database.
    /// </summary>
    public IClipRepository GetClipRepository(string databaseKey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var databasePath = ResolveDatabaseKeyToPath(databaseKey);
        var context = CreateContext(databasePath);

        return ActivatorUtilities.CreateInstance<ClipRepository>(_serviceProvider, context) as IClipRepository
               ?? throw new InvalidOperationException("Failed to create ClipRepository instance.");
    }

    /// <summary>
    /// Creates a ClipDataRepository instance for the specified database.
    /// </summary>
    public IClipDataRepository GetClipDataRepository(string databaseKey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var databasePath = ResolveDatabaseKeyToPath(databaseKey);
        var context = CreateContext(databasePath);

        return new ClipDataRepository(context);
    }

    /// <summary>
    /// Creates a BlobRepository instance for the specified database.
    /// </summary>
    public IBlobRepository GetBlobRepository(string databaseKey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var databasePath = ResolveDatabaseKeyToPath(databaseKey);
        var context = CreateContext(databasePath);

        return new BlobRepository(context);
    }

    /// <summary>
    /// Creates a ShortcutRepository instance for the specified database.
    /// </summary>
    public IShortcutRepository GetShortcutRepository(string databaseKey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var databasePath = ResolveDatabaseKeyToPath(databaseKey);
        var context = CreateContext(databasePath);

        return ActivatorUtilities.CreateInstance<ShortcutRepository>(_serviceProvider, context) as IShortcutRepository
               ?? throw new InvalidOperationException("Failed to create ShortcutRepository instance.");
    }

    /// <summary>
    /// Creates a UserRepository instance for the specified database.
    /// </summary>
    public IUserRepository GetUserRepository(string databaseKey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var databasePath = ResolveDatabaseKeyToPath(databaseKey);
        var context = CreateContext(databasePath);

        return ActivatorUtilities.CreateInstance<UserRepository>(_serviceProvider, context) as IUserRepository
               ?? throw new InvalidOperationException("Failed to create UserRepository instance.");
    }

    /// <summary>
    /// Creates a CollectionRepository instance for the specified database.
    /// </summary>
    public ICollectionRepository GetCollectionRepository(string databaseKey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var databasePath = ResolveDatabaseKeyToPath(databaseKey);
        var context = CreateContext(databasePath);

        return ActivatorUtilities.CreateInstance<CollectionRepository>(_serviceProvider, context) as ICollectionRepository
               ?? throw new InvalidOperationException("Failed to create CollectionRepository instance.");
    }

    /// <summary>
    /// Creates a FolderRepository instance for the specified database.
    /// </summary>
    public IFolderRepository GetFolderRepository(string databaseKey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var databasePath = ResolveDatabaseKeyToPath(databaseKey);
        var context = CreateContext(databasePath);

        return ActivatorUtilities.CreateInstance<FolderRepository>(_serviceProvider, context) as IFolderRepository
               ?? throw new InvalidOperationException("Failed to create FolderRepository instance.");
    }

    /// <summary>
    /// Creates an ApplicationFilterRepository instance for the specified database.
    /// </summary>
    public IApplicationFilterRepository GetApplicationFilterRepository(string databaseKey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var databasePath = ResolveDatabaseKeyToPath(databaseKey);
        var context = CreateContext(databasePath);

        return ActivatorUtilities.CreateInstance<ApplicationFilterRepository>(_serviceProvider, context) as IApplicationFilterRepository
               ?? throw new InvalidOperationException("Failed to create ApplicationFilterRepository instance.");
    }

    /// <summary>
    /// Creates a TemplateRepository instance for the specified database.
    /// </summary>
    public ITemplateRepository GetTemplateRepository(string databaseKey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var databasePath = ResolveDatabaseKeyToPath(databaseKey);
        var context = CreateContext(databasePath);

        return ActivatorUtilities.CreateInstance<TemplateRepository>(_serviceProvider, context) as ITemplateRepository
               ?? throw new InvalidOperationException("Failed to create TemplateRepository instance.");
    }

    /// <summary>
    /// Creates a SearchQueryRepository instance for the specified database.
    /// </summary>
    public ISearchQueryRepository GetSearchQueryRepository(string databaseKey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var databasePath = ResolveDatabaseKeyToPath(databaseKey);
        var context = CreateContext(databasePath);

        return ActivatorUtilities.CreateInstance<SearchQueryRepository>(_serviceProvider, context) as ISearchQueryRepository
               ?? throw new InvalidOperationException("Failed to create SearchQueryRepository instance.");
    }

    /// <summary>
    /// Creates a MonacoEditorStateRepository instance for the specified database.
    /// </summary>
    public IMonacoEditorStateRepository GetMonacoEditorStateRepository(string databaseKey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var databasePath = ResolveDatabaseKeyToPath(databaseKey);
        var context = CreateContext(databasePath);

        return new MonacoEditorStateRepository(context);
    }

    /// <summary>
    /// Disposes the factory. Note: DbContext instances are not cached,
    /// so callers are responsible for disposing their own contexts.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _logger.LogInformation("Disposing DatabaseContextFactory ({Count} databases registered)", _loadedDatabases.Count);

        lock (_lock)
        {
            _loadedDatabases.Clear();
        }

        _disposed = true;
    }

    /// <summary>
    /// Resolves a database key to its file path using configuration.
    /// If the input is already a file path (contains path separators), returns it as-is.
    /// </summary>
    private string ResolveDatabaseKeyToPath(string databaseKeyOrPath)
    {
        // If it looks like a file path (contains directory separators), return as-is
        if (databaseKeyOrPath.Contains(Path.DirectorySeparatorChar) ||
            databaseKeyOrPath.Contains(Path.AltDirectorySeparatorChar))
            return databaseKeyOrPath;

        // Otherwise, try to resolve it as a database key from configuration
        if (_configurationService.Configuration.Databases.TryGetValue(databaseKeyOrPath, out var dbConfig))
            return dbConfig.FilePath;

        // If not found in configuration, assume it's a path anyway
        _logger.LogWarning("Database key '{DatabaseKey}' not found in configuration, treating as file path", databaseKeyOrPath);
        return databaseKeyOrPath;
    }
}
