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
    /// Gets all collections.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all collections.</returns>
    Task<IReadOnlyList<Collection>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the currently active collection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The active collection; throws if none active.</returns>
    Task<Collection> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new collection.
    /// </summary>
    /// <param name="name">Collection name.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created collection.</returns>
    Task<Collection> CreateAsync(string name, string? description = null, CancellationToken cancellationToken = default);

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
    /// Sets the active collection.
    /// </summary>
    /// <param name="id">The collection ID to activate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetActiveAsync(Guid id, CancellationToken cancellationToken = default);
}
