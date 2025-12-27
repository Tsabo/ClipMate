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
    /// <param name="databasePath">Path to the database to close.</param>
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
