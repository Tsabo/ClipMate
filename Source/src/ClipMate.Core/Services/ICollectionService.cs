using ClipMate.Core.Models;

namespace ClipMate.Core.Services;

/// <summary>
/// Service for managing collections.
/// </summary>
public interface ICollectionService
{
    /// <summary>
    /// Gets a collection by ID.
    /// </summary>
    /// <param name="id">The collection ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The collection if found; otherwise, null.</returns>
    Task<Collection?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all collections from the active database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all collections.</returns>
    Task<IReadOnlyList<Collection>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all collections from the specified database.
    /// </summary>
    /// <param name="databaseKey">The database key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all collections in the specified database.</returns>
    Task<IReadOnlyList<Collection>> GetAllByDatabaseKeyAsync(string databaseKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the currently active collection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The active collection; throws if none active.</returns>
    Task<Collection> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the first collection (ordered by SortKey) that accepts new clips.
    /// Used for bounce tracking when active collection has AcceptNewClips=false.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The first accepting collection, or null if none found.</returns>
    Task<Collection?> GetFirstAcceptingCollectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the database key for the currently active collection.
    /// </summary>
    /// <returns>The database key, or null if no active collection.</returns>
    string? GetActiveDatabaseKey();

    /// <summary>
    /// Creates a new collection.
    /// </summary>
    /// <param name="name">Collection name.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created collection.</returns>
    Task<Collection> CreateAsync(string name, string? description = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the active collection.
    /// </summary>
    /// <param name="id">The collection ID to set as active.</param>
    /// <param name="databaseKey">The database key where this collection resides.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetActiveAsync(Guid id, string databaseKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing collection.
    /// </summary>
    /// <param name="collection">The collection to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateAsync(Collection collection, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a collection and all its clips/folders.
    /// </summary>
    /// <param name="id">The collection ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of non-deleted clips in a collection.
    /// </summary>
    /// <param name="collectionId">The collection ID.</param>
    /// <param name="databaseKey">The database key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The count of clips.</returns>
    Task<int> GetCollectionItemCountAsync(Guid collectionId, string databaseKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a collection up in sort order (decreases SortKey).
    /// </summary>
    /// <param name="collectionId">The collection ID to move.</param>
    /// <param name="databaseKey">The database key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if moved, false if already at top or not found.</returns>
    Task<bool> MoveCollectionUpAsync(Guid collectionId, string databaseKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a collection down in sort order (increases SortKey).
    /// </summary>
    /// <param name="collectionId">The collection ID to move.</param>
    /// <param name="databaseKey">The database key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if moved, false if already at bottom or not found.</returns>
    Task<bool> MoveCollectionDownAsync(Guid collectionId, string databaseKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reorders collections by inserting dropped collections at a target position.
    /// </summary>
    /// <param name="droppedCollectionIds">IDs of collections to move.</param>
    /// <param name="targetCollectionId">ID of collection to insert near.</param>
    /// <param name="insertAfter">True to insert after target, false to insert before.</param>
    /// <param name="databaseKey">The database key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReorderCollectionsAsync(List<Guid> droppedCollectionIds,
        Guid targetCollectionId,
        bool insertAfter,
        string databaseKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resequences sort keys for all collections in the specified database.
    /// Normalizes SortKey values to 10, 20, 30, etc. for cleaner ordering.
    /// </summary>
    /// <param name="databaseKey">The database key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of collections updated.</returns>
    Task<int> ResequenceSortKeysAsync(string databaseKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the first collection marked as favorite in the specified database.
    /// </summary>
    /// <param name="databaseKey">The database key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The favorite collection, or null if none is marked as favorite.</returns>
    Task<Collection?> GetFavoriteCollectionAsync(string databaseKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new collection with optional parent.
    /// </summary>
    /// <param name="name">Collection name.</param>
    /// <param name="parentId">Optional parent collection/folder ID.</param>
    /// <param name="databaseKey">The database key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created collection.</returns>
    Task<Collection> CreateAsync(string name, Guid? parentId, string databaseKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a collection by name from the specified database.
    /// </summary>
    /// <param name="name">The collection name to find.</param>
    /// <param name="databaseKey">The database key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The collection if found; otherwise, null.</returns>
    Task<Collection?> GetByNameAsync(string name, string databaseKey, CancellationToken cancellationToken = default);
}
