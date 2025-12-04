using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data;

/// <summary>
/// Factory for creating ClipMateDbContext instances for multiple databases.
/// Supports the multi-database architecture from ClipMate 7.5.
/// </summary>
public class DatabaseContextFactory : IDatabaseContextFactory
{
    private readonly Dictionary<string, ClipMateDbContext> _contexts;
    private readonly ILogger<DatabaseContextFactory> _logger;
    private bool _disposed;

    public DatabaseContextFactory(ILogger<DatabaseContextFactory> logger)
    {
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

        if (_contexts.Remove(normalizedPath, out var context))
        {
            _logger.LogInformation("Closing database: {DatabasePath}", normalizedPath);
            context.Dispose();

            return true;
        }

        return false;
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
