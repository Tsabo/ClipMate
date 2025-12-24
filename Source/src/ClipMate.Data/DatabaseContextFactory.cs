using ClipMate.Core.Repositories;
using ClipMate.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data;

/// <summary>
/// Factory for creating ClipMateDbContext instances for multiple databases
/// and database-specific repository instances.
/// Supports the multi-database architecture from ClipMate 7.5.
/// </summary>
public class DatabaseContextFactory : IDatabaseContextFactory
{
    private readonly Dictionary<string, ClipMateDbContext> _contexts;
    private readonly ILogger<DatabaseContextFactory> _logger;
    private readonly IServiceProvider _serviceProvider;
    private bool _disposed;

    public DatabaseContextFactory(IServiceProvider serviceProvider,
        ILogger<DatabaseContextFactory> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _contexts = new Dictionary<string, ClipMateDbContext>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets or creates a database context for the specified database path.
    /// </summary>
    /// <param name="databasePath">Full path to the SQLite database file.</param>
    /// <returns>A ClipMateDbContext instance for the database.</returns>
    public ClipMateDbContext GetOrCreateContext(string databasePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("Database path cannot be null or empty.", nameof(databasePath));

        var normalizedPath = Path.GetFullPath(databasePath);

        if (_contexts.TryGetValue(normalizedPath, out var existingContext))
        {
            _logger.LogDebug("Returning existing context for database: {DatabasePath}", normalizedPath);

            return existingContext;
        }

        _logger.LogInformation("Creating new context for database: {DatabasePath}", normalizedPath);

        var optionsBuilder = new DbContextOptionsBuilder<ClipMateDbContext>();
        optionsBuilder.UseSqlite($"Data Source={normalizedPath}");

        var context = new ClipMateDbContext(optionsBuilder.Options);
        _contexts[normalizedPath] = context;

        return context;
    }

    /// <summary>
    /// Gets all active database contexts.
    /// </summary>
    /// <returns>Collection of all active contexts.</returns>
    public IReadOnlyCollection<ClipMateDbContext> GetAllContexts()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _contexts.Values.ToList();
    }

    /// <summary>
    /// Gets all database paths currently loaded.
    /// </summary>
    /// <returns>Collection of database paths.</returns>
    public IReadOnlyCollection<string> GetLoadedDatabasePaths()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _contexts.Keys.ToList();
    }

    /// <summary>
    /// Removes and disposes a database context.
    /// </summary>
    /// <param name="databasePath">Path to the database to close.</param>
    /// <returns>True if the context was found and disposed.</returns>
    public bool CloseDatabase(string databasePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var normalizedPath = Path.GetFullPath(databasePath);

        if (!_contexts.Remove(normalizedPath, out var context))
            return false;

        _logger.LogInformation("Closing database: {DatabasePath}", normalizedPath);
        context.Dispose();

        return true;
    }

    /// <summary>
    /// Creates a ClipRepository instance for the specified database context.
    /// </summary>
    public IClipRepository GetClipRepository(ClipMateDbContext context)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return context == null
            ? throw new ArgumentNullException(nameof(context))
            : ActivatorUtilities.CreateInstance<ClipRepository>(_serviceProvider, context) as IClipRepository
              ?? throw new InvalidOperationException("Failed to create ClipRepository instance.");
    }

    /// <summary>
    /// Creates a ClipDataRepository instance for the specified database context.
    /// </summary>
    public IClipDataRepository GetClipDataRepository(ClipMateDbContext context)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return context == null
            ? throw new ArgumentNullException(nameof(context))
            : ActivatorUtilities.CreateInstance<ClipDataRepository>(_serviceProvider, context) as IClipDataRepository
              ?? throw new InvalidOperationException("Failed to create ClipDataRepository instance.");
    }

    /// <summary>
    /// Creates a BlobRepository instance for the specified database context.
    /// </summary>
    public IBlobRepository GetBlobRepository(ClipMateDbContext context)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return context == null
            ? throw new ArgumentNullException(nameof(context))
            : ActivatorUtilities.CreateInstance<BlobRepository>(_serviceProvider, context) as IBlobRepository
              ?? throw new InvalidOperationException("Failed to create BlobRepository instance.");
    }

    /// <summary>
    /// Creates a ShortcutRepository instance for the specified database context.
    /// </summary>
    public IShortcutRepository GetShortcutRepository(ClipMateDbContext context)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return context == null
            ? throw new ArgumentNullException(nameof(context))
            : ActivatorUtilities.CreateInstance<ShortcutRepository>(_serviceProvider, context) as IShortcutRepository
              ?? throw new InvalidOperationException("Failed to create ShortcutRepository instance.");
    }

    /// <summary>
    /// Creates a UserRepository instance for the specified database context.
    /// </summary>
    public IUserRepository GetUserRepository(ClipMateDbContext context)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return context == null
            ? throw new ArgumentNullException(nameof(context))
            : ActivatorUtilities.CreateInstance<UserRepository>(_serviceProvider, context) as IUserRepository
              ?? throw new InvalidOperationException("Failed to create UserRepository instance.");
    }

    /// <summary>
    /// Creates a MonacoEditorStateRepository instance for the specified database context.
    /// </summary>
    public IMonacoEditorStateRepository GetMonacoEditorStateRepository(ClipMateDbContext context)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (context == null)
            throw new ArgumentNullException(nameof(context));

        return ActivatorUtilities.CreateInstance<MonacoEditorStateRepository>(_serviceProvider, context) as IMonacoEditorStateRepository
               ?? throw new InvalidOperationException("Failed to create MonacoEditorStateRepository instance.");
    }

    /// <summary>
    /// Disposes all database contexts.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _logger.LogInformation("Disposing all database contexts ({Count} contexts)", _contexts.Count);

        foreach (var context in _contexts.Values)
        {
            try
            {
                context.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing database context");
            }
        }

        _contexts.Clear();
        _disposed = true;
    }
}
