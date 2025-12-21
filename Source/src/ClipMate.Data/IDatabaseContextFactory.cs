using ClipMate.Core.Repositories;

namespace ClipMate.Data;

/// <summary>
/// Interface for factory that creates ClipMateDbContext instances for multiple databases
/// and provides database-specific repository instances.
/// </summary>
public interface IDatabaseContextFactory : IDisposable
{
    /// <summary>
    /// Gets or creates a database context for the specified database path.
    /// </summary>
    /// <param name="databasePath">Full path to the SQLite database file.</param>
    /// <returns>A ClipMateDbContext instance for the database.</returns>
    ClipMateDbContext GetOrCreateContext(string databasePath);

    /// <summary>
    /// Closes and removes a database context.
    /// </summary>
    /// <param name=\"databasePath\">Path to the database to close.</param>
    /// <returns>True if the context was found and disposed.</returns>
    bool CloseDatabase(string databasePath);

    /// <summary>
    /// Gets all currently open database contexts.
    /// </summary>
    /// <returns>Collection of all open database contexts.</returns>
    IReadOnlyCollection<ClipMateDbContext> GetAllContexts();

    /// <summary>
    /// Gets all database paths currently loaded.
    /// </summary>
    /// <returns>Collection of database paths.</returns>
    IReadOnlyCollection<string> GetLoadedDatabasePaths();

    /// <summary>
    /// Creates a ClipDataRepository instance for the specified database context.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <returns>A repository instance bound to the specified database context.</returns>
    IClipDataRepository GetClipDataRepository(ClipMateDbContext context);

    /// <summary>
    /// Creates a BlobRepository instance for the specified database context.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <returns>A repository instance bound to the specified database context.</returns>
    IBlobRepository GetBlobRepository(ClipMateDbContext context);

    /// <summary>
    /// Creates a ShortcutRepository instance for the specified database context.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <returns>A repository instance bound to the specified database context.</returns>
    IShortcutRepository GetShortcutRepository(ClipMateDbContext context);

    /// <summary>
    /// Creates a UserRepository instance for the specified database context.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <returns>A repository instance bound to the specified database context.</returns>
    IUserRepository GetUserRepository(ClipMateDbContext context);

    /// <summary>
    /// Creates a MonacoEditorStateRepository instance for the specified database context.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <returns>A repository instance bound to the specified database context.</returns>
    IMonacoEditorStateRepository GetMonacoEditorStateRepository(ClipMateDbContext context);
}
