using ClipMate.Core.Models;

namespace ClipMate.Core.Services;

/// <summary>
/// Service for managing clips (CRUD operations, history management).
/// </summary>
public interface IClipService
{
    /// <summary>
    /// Gets a clip by ID.
    /// </summary>
    /// <param name="id">The clip ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The clip if found; otherwise, null.</returns>
    Task<Clip?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent clips, ordered by captured date descending.
    /// </summary>
    /// <param name="count">Maximum number of clips to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of recent clips.</returns>
    Task<IReadOnlyList<Clip>> GetRecentAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets clips in a collection.
    /// </summary>
    /// <param name="collectionId">The collection ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of clips in the collection.</returns>
    Task<IReadOnlyList<Clip>> GetByCollectionAsync(Guid collectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets clips in a folder.
    /// </summary>
    /// <param name="folderId">The folder ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of clips in the folder.</returns>
    Task<IReadOnlyList<Clip>> GetByFolderAsync(Guid folderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets favorite clips.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of favorite clips.</returns>
    Task<IReadOnlyList<Clip>> GetFavoritesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new clip.
    /// </summary>
    /// <param name="clip">The clip to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created clip with generated ID.</returns>
    Task<Clip> CreateAsync(Clip clip, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing clip.
    /// </summary>
    /// <param name="clip">The clip to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateAsync(Clip clip, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a clip.
    /// </summary>
    /// <param name="id">The clip ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes clips older than the specified date.
    /// </summary>
    /// <param name="olderThan">Delete clips captured before this date.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of clips deleted.</returns>
    Task<int> DeleteOlderThanAsync(DateTime olderThan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a clip with the same content hash already exists.
    /// </summary>
    /// <param name="contentHash">The content hash to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if duplicate exists; otherwise, false.</returns>
    Task<bool> IsDuplicateAsync(string contentHash, CancellationToken cancellationToken = default);
}
