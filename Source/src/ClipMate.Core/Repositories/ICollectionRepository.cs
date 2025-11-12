using ClipMate.Core.Models;

namespace ClipMate.Core.Repositories;

/// <summary>
/// Repository interface for managing Collection entities in the data store.
/// </summary>
public interface ICollectionRepository
{
    /// <summary>
    /// Retrieves a collection by its unique identifier.
    /// </summary>
    /// <param name="id">The collection's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The collection if found; otherwise, null.</returns>
    Task<Collection?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all collections, ordered by sort order.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all collections.</returns>
    Task<IReadOnlyList<Collection>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the currently active collection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The active collection if found; otherwise, null.</returns>
    Task<Collection?> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new collection in the data store.
    /// </summary>
    /// <param name="collection">The collection to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created collection with generated ID.</returns>
    Task<Collection> CreateAsync(Collection collection, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing collection in the data store.
    /// </summary>
    /// <param name="collection">The collection to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if updated successfully; otherwise, false.</returns>
    Task<bool> UpdateAsync(Collection collection, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a collection from the data store.
    /// </summary>
    /// <param name="id">The collection's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted successfully; otherwise, false.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
