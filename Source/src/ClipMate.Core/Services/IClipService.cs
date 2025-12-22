using ClipMate.Core.Models;

namespace ClipMate.Core.Services;

/// <summary>
/// Service for managing clips (CRUD operations, history management).
/// Supports multi-database operations via database key parameters.
/// </summary>
public interface IClipService
{
    /// <summary>
    /// Event raised when a new clip is added to the database.
    /// </summary>
    event EventHandler<Clip>? ClipAdded;

    /// <summary>
    /// Gets a clip by ID from the specified database.
    /// </summary>
    /// <param name="databaseKey">The database key (path).</param>
    /// <param name="id">The clip ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The clip if found; otherwise, null.</returns>
    Task<Clip?> GetByIdAsync(string databaseKey, Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent clips from the specified database, ordered by captured date descending.
    /// </summary>
    /// <param name="databaseKey">The database key (path).</param>
    /// <param name="count">Maximum number of clips to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of recent clips.</returns>
    Task<IReadOnlyList<Clip>> GetRecentAsync(string databaseKey, int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets clips in a collection from the specified database.
    /// </summary>
    /// <param name="databaseKey">The database key (path).</param>
    /// <param name="collectionId">The collection ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of clips in the collection.</returns>
    Task<IReadOnlyList<Clip>> GetByCollectionAsync(string databaseKey, Guid collectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets clips in a folder from the specified database.
    /// </summary>
    /// <param name="databaseKey">The database key (path).</param>
    /// <param name="folderId">The folder ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of clips in the folder.</returns>
    Task<IReadOnlyList<Clip>> GetByFolderAsync(string databaseKey, Guid folderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets favorite clips from the specified database.
    /// </summary>
    /// <param name="databaseKey">The database key (path).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of favorite clips.</returns>
    Task<IReadOnlyList<Clip>> GetFavoritesAsync(string databaseKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new clip in the specified database.
    /// </summary>
    /// <param name="databaseKey">The database key (path).</param>
    /// <param name="clip">The clip to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created clip with generated ID.</returns>
    Task<Clip> CreateAsync(string databaseKey, Clip clip, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing clip in the specified database.
    /// </summary>
    /// <param name="databaseKey">The database key (path).</param>
    /// <param name="clip">The clip to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateAsync(string databaseKey, Clip clip, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a clip from the specified database.
    /// </summary>
    /// <param name="databaseKey">The database key (path).</param>
    /// <param name="id">The clip ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(string databaseKey, Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes clips older than the specified date from the specified database.
    /// </summary>
    /// <param name="databaseKey">The database key (path).</param>
    /// <param name="olderThan">Delete clips captured before this date.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of clips deleted.</returns>
    Task<int> DeleteOlderThanAsync(string databaseKey, DateTime olderThan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a clip with the same content hash already exists in the specified database.
    /// </summary>
    /// <param name="databaseKey">The database key (path).</param>
    /// <param name="contentHash">The content hash to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if duplicate exists; otherwise, false.</returns>
    Task<bool> IsDuplicateAsync(string databaseKey, string contentHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the clipboard format data for a clip from the specified database.
    /// </summary>
    /// <param name="databaseKey">The database key (path).</param>
    /// <param name="clipId">The clip ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of clipboard formats stored for the clip.</returns>
    Task<IReadOnlyList<ClipData>> GetClipFormatsAsync(string databaseKey, Guid clipId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames a clip by changing its title in the specified database.
    /// </summary>
    /// <param name="databaseKey">The database key (path).</param>
    /// <param name="clipId">The clip ID.</param>
    /// <param name="newTitle">The new title for the clip.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if renamed successfully; otherwise, false.</returns>
    Task<bool> RenameClipAsync(string databaseKey, Guid clipId, string newTitle, CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies a clip to a different collection within the same database.
    /// Creates a new clip with same content in target collection.
    /// </summary>
    /// <param name="databaseKey">The database key (path).</param>
    /// <param name="sourceClipId">The source clip ID to copy from.</param>
    /// <param name="targetCollectionId">The target collection ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The newly created clip copy.</returns>
    Task<Clip> CopyClipAsync(string databaseKey, Guid sourceClipId, Guid targetCollectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a clip to a different collection within the same database.
    /// Updates the clip's collection ID.
    /// </summary>
    /// <param name="databaseKey">The database key (path).</param>
    /// <param name="clipId">The clip ID to move.</param>
    /// <param name="targetCollectionId">The target collection ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if moved successfully; otherwise, false.</returns>
    Task<bool> MoveClipAsync(string databaseKey, Guid clipId, Guid targetCollectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies a clip from one database to another database.
    /// Creates a new clip with same content in the target database and collection.
    /// </summary>
    /// <param name="sourceDatabaseKey">The source database key (path).</param>
    /// <param name="sourceClipId">The source clip ID to copy from.</param>
    /// <param name="targetDatabaseKey">The target database key (path).</param>
    /// <param name="targetCollectionId">The target collection ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The newly created clip copy in the target database.</returns>
    Task<Clip> CopyClipCrossDatabaseAsync(string sourceDatabaseKey, Guid sourceClipId, string targetDatabaseKey, Guid targetCollectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a clip from one database to another database.
    /// Creates a copy in the target database and deletes the original.
    /// </summary>
    /// <param name="sourceDatabaseKey">The source database key (path).</param>
    /// <param name="sourceClipId">The source clip ID to move.</param>
    /// <param name="targetDatabaseKey">The target database key (path).</param>
    /// <param name="targetCollectionId">The target collection ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The newly created clip in the target database.</returns>
    Task<Clip> MoveClipCrossDatabaseAsync(string sourceDatabaseKey, Guid sourceClipId, string targetDatabaseKey, Guid targetCollectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a clip's full content from the database and sets it to the Windows clipboard.
    /// This method loads all clip data (text, images, files) and updates the system clipboard.
    /// Used for "Pick, Flip, and Paste" functionality and after editing clips.
    /// </summary>
    /// <param name="databaseKey">The database key (path).</param>
    /// <param name="clipId">The clip ID to load and set to clipboard.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task LoadAndSetClipboardAsync(string databaseKey, Guid clipId, CancellationToken cancellationToken = default);
}
