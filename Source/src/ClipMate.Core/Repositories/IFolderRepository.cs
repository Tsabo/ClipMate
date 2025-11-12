using ClipMate.Core.Models;

namespace ClipMate.Core.Repositories;

/// <summary>
/// Repository interface for managing Folder entities in the data store.
/// </summary>
public interface IFolderRepository
{
    /// <summary>
    /// Retrieves a folder by its unique identifier.
    /// </summary>
    /// <param name="id">The folder's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The folder if found; otherwise, null.</returns>
    Task<Folder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all folders for a specific collection, ordered by sort order.
    /// </summary>
    /// <param name="collectionId">The collection's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of folders in the collection.</returns>
    Task<IReadOnlyList<Folder>> GetByCollectionAsync(Guid collectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves child folders of a specific parent folder.
    /// </summary>
    /// <param name="parentFolderId">The parent folder's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of child folders.</returns>
    Task<IReadOnlyList<Folder>> GetChildFoldersAsync(Guid parentFolderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves root-level folders for a collection (folders with no parent).
    /// </summary>
    /// <param name="collectionId">The collection's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of root-level folders.</returns>
    Task<IReadOnlyList<Folder>> GetRootFoldersAsync(Guid collectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new folder in the data store.
    /// </summary>
    /// <param name="folder">The folder to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created folder with generated ID.</returns>
    Task<Folder> CreateAsync(Folder folder, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing folder in the data store.
    /// </summary>
    /// <param name="folder">The folder to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if updated successfully; otherwise, false.</returns>
    Task<bool> UpdateAsync(Folder folder, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a folder from the data store.
    /// </summary>
    /// <param name="id">The folder's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted successfully; otherwise, false.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
