using ClipMate.Core.Models;

namespace ClipMate.Core.Repositories;

/// <summary>
/// Repository interface for managing Clip entities in the data store.
/// </summary>
public interface IClipRepository
{
    /// <summary>
    /// Retrieves a clip by its unique identifier.
    /// </summary>
    /// <param name="id">The clip's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The clip if found; otherwise, null.</returns>
    Task<Clip?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a clip by its content hash (for duplicate detection).
    /// </summary>
    /// <param name="contentHash">The SHA256 content hash.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The clip if found; otherwise, null.</returns>
    Task<Clip?> GetByContentHashAsync(string contentHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the most recent clips, ordered by captured date descending.
    /// </summary>
    /// <param name="count">Maximum number of clips to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of recent clips.</returns>
    Task<IReadOnlyList<Clip>> GetRecentAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves clips for a specific collection.
    /// </summary>
    /// <param name="collectionId">The collection's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of clips in the collection.</returns>
    Task<IReadOnlyList<Clip>> GetByCollectionAsync(Guid collectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves clips for a specific folder.
    /// </summary>
    /// <param name="folderId">The folder's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of clips in the folder.</returns>
    Task<IReadOnlyList<Clip>> GetByFolderAsync(Guid folderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves favorite clips.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of favorite clips.</returns>
    Task<IReadOnlyList<Clip>> GetFavoritesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches clips by text content.
    /// </summary>
    /// <param name="searchText">The text to search for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of matching clips.</returns>
    Task<IReadOnlyList<Clip>> SearchAsync(string searchText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new clip in the data store.
    /// </summary>
    /// <param name="clip">The clip to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created clip with generated ID.</returns>
    Task<Clip> CreateAsync(Clip clip, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing clip in the data store.
    /// </summary>
    /// <param name="clip">The clip to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if updated successfully; otherwise, false.</returns>
    Task<bool> UpdateAsync(Clip clip, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a clip from the data store.
    /// </summary>
    /// <param name="id">The clip's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted successfully; otherwise, false.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all clips older than the specified date.
    /// </summary>
    /// <param name="olderThan">Delete clips captured before this date.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of clips deleted.</returns>
    Task<int> DeleteOlderThanAsync(DateTime olderThan, CancellationToken cancellationToken = default);
}
