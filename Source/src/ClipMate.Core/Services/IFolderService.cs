using ClipMate.Core.Models;

namespace ClipMate.Core.Services;

/// <summary>
/// Service for managing folders within collections.
/// </summary>
public interface IFolderService
{
    /// <summary>
    /// Gets a folder by ID.
    /// </summary>
    /// <param name="id">The folder ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The folder if found; otherwise, null.</returns>
    Task<Folder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all folders in a collection.
    /// </summary>
    /// <param name="collectionId">The collection ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of folders in the collection.</returns>
    Task<IReadOnlyList<Folder>> GetByCollectionAsync(Guid collectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets root-level folders (no parent) in a collection.
    /// </summary>
    /// <param name="collectionId">The collection ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of root folders.</returns>
    Task<IReadOnlyList<Folder>> GetRootFoldersAsync(Guid collectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets child folders of a parent folder.
    /// </summary>
    /// <param name="parentFolderId">The parent folder ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of child folders.</returns>
    Task<IReadOnlyList<Folder>> GetChildFoldersAsync(Guid parentFolderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new folder.
    /// </summary>
    /// <param name="name">Folder name.</param>
    /// <param name="collectionId">The collection ID.</param>
    /// <param name="parentFolderId">Optional parent folder ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created folder.</returns>
    Task<Folder> CreateAsync(string name, Guid collectionId, Guid? parentFolderId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing folder.
    /// </summary>
    /// <param name="folder">The folder to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateAsync(Folder folder, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a folder and all its clips/subfolders.
    /// </summary>
    /// <param name="id">The folder ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
