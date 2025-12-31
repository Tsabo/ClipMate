using ClipMate.Core.Repositories;

namespace ClipMate.Data;

/// <summary>
/// Interface for factory that creates ClipMateDbContext instances for multiple databases
/// and provides database-specific repository instances.
/// DbContext instances are NOT cached - each call creates a fresh, short-lived context
/// that should be disposed after use (EF Core best practice for thread safety).
/// </summary>
public interface IDatabaseContextFactory : IDisposable
{
    /// <summary>
    /// Creates a new database context for the specified database path.
    /// The context is NOT cached - caller is responsible for disposal.
    /// Also registers the database path as "loaded" if not already registered.
    /// </summary>
    /// <param name="databasePath">Full path to the SQLite database file.</param>
    /// <returns>A new ClipMateDbContext instance for the database.</returns>
    ClipMateDbContext CreateContext(string databasePath);

    /// <summary>
    /// Registers a database path as "loaded" without creating a context.
    /// Use this when you need to track that a database is available but don't need a context yet.
    /// </summary>
    /// <param name="databasePath">Full path to the SQLite database file.</param>
    void RegisterDatabase(string databasePath);

    /// <summary>
    /// Unregisters a database path, marking it as no longer loaded.
    /// </summary>
    /// <param name="databasePath">Path to the database to unregister.</param>
    /// <returns>True if the database was registered and has been unregistered.</returns>
    bool CloseDatabase(string databasePath);

    /// <summary>
    /// Gets all database paths currently registered as loaded.
    /// </summary>
    /// <returns>Collection of database paths.</returns>
    IReadOnlyCollection<string> GetLoadedDatabasePaths();

    /// <summary>
    /// Creates a ClipRepository instance for the specified database.
    /// </summary>
    /// <param name="databaseKey">The database key (path).</param>
    /// <returns>A repository instance bound to the specified database.</returns>
    IClipRepository GetClipRepository(string databaseKey);

    /// <summary>
    /// Creates a ClipDataRepository instance for the specified database.
    /// </summary>
    /// <param name="databaseKey">The database key (path).</param>
    /// <returns>A repository instance bound to the specified database.</returns>
    IClipDataRepository GetClipDataRepository(string databaseKey);

    /// <summary>
    /// Creates a BlobRepository instance for the specified database.
    /// </summary>
    /// <param name="databaseKey">The database key (path).</param>
    /// <returns>A repository instance bound to the specified database.</returns>
    IBlobRepository GetBlobRepository(string databaseKey);

    /// <summary>
    /// Creates a ShortcutRepository instance for the specified database.
    /// </summary>
    /// <param name="databaseKey">The database key (path).</param>
    /// <returns>A repository instance bound to the specified database.</returns>
    IShortcutRepository GetShortcutRepository(string databaseKey);

    /// <summary>
    /// Creates a UserRepository instance for the specified database.
    /// </summary>
    /// <param name="databaseKey">The database key (path).</param>
    /// <returns>A repository instance bound to the specified database.</returns>
    IUserRepository GetUserRepository(string databaseKey);

    /// <summary>
    /// Creates a MonacoEditorStateRepository instance for the specified database.
    /// </summary>
    /// <param name="databaseKey">The database key (path).</param>
    /// <returns>A repository instance bound to the specified database.</returns>
    IMonacoEditorStateRepository GetMonacoEditorStateRepository(string databaseKey);

    /// <summary>
    /// Creates a CollectionRepository instance for the specified database.
    /// </summary>
    /// <param name="databaseKey">The database key (path).</param>
    /// <returns>A repository instance bound to the specified database.</returns>
    ICollectionRepository GetCollectionRepository(string databaseKey);

    /// <summary>
    /// Creates a FolderRepository instance for the specified database.
    /// </summary>
    /// <param name="databaseKey">The database key (path).</param>
    /// <returns>A repository instance bound to the specified database.</returns>
    IFolderRepository GetFolderRepository(string databaseKey);

    /// <summary>
    /// Creates an ApplicationFilterRepository instance for the specified database.
    /// </summary>
    /// <param name="databaseKey">The database key (path).</param>
    /// <returns>A repository instance bound to the specified database.</returns>
    IApplicationFilterRepository GetApplicationFilterRepository(string databaseKey);

    /// <summary>
    /// Creates a TemplateRepository instance for the specified database.
    /// </summary>
    /// <param name="databaseKey">The database key (path).</param>
    /// <returns>A repository instance bound to the specified database.</returns>
    ITemplateRepository GetTemplateRepository(string databaseKey);

    /// <summary>
    /// Creates a SearchQueryRepository instance for the specified database.
    /// </summary>
    /// <param name="databaseKey">The database key (path).</param>
    /// <returns>A repository instance bound to the specified database.</returns>
    ISearchQueryRepository GetSearchQueryRepository(string databaseKey);
}
