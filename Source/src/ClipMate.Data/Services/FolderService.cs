using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;

namespace ClipMate.Data.Services;

/// <summary>
/// Service for managing folders within collections.
/// </summary>
public class FolderService : IFolderService
{
    private readonly IFolderRepository _repository;
    private Guid? _activeFolderId;

    public FolderService(IFolderRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<Folder> CreateAsync(string name, Guid collectionId, Guid? parentFolderId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var folder = new Folder
        {
            Id = Guid.NewGuid(),
            Name = name,
            CollectionId = collectionId,
            ParentFolderId = parentFolderId,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };

        return await _repository.CreateAsync(folder, cancellationToken);
    }

    public async Task<Folder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _repository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<Folder>> GetByCollectionAsync(Guid collectionId, CancellationToken cancellationToken = default)
    {
        return await _repository.GetByCollectionAsync(collectionId, cancellationToken);
    }

    public async Task<IReadOnlyList<Folder>> GetRootFoldersAsync(Guid collectionId, CancellationToken cancellationToken = default)
    {
        return await _repository.GetRootFoldersAsync(collectionId, cancellationToken);
    }

    public async Task<IReadOnlyList<Folder>> GetChildFoldersAsync(Guid parentFolderId, CancellationToken cancellationToken = default)
    {
        return await _repository.GetChildFoldersAsync(parentFolderId, cancellationToken);
    }

    public async Task UpdateAsync(Folder folder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(folder);
        folder.ModifiedAt = DateTime.UtcNow;
        var updated = await _repository.UpdateAsync(folder, cancellationToken);
        if (!updated)
        {
            throw new InvalidOperationException($"Failed to update folder {folder.Id}.");
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var deleted = await _repository.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            throw new InvalidOperationException($"Failed to delete folder {id}.");
        }
    }

    public Task<Folder?> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        if (_activeFolderId == null)
        {
            return Task.FromResult<Folder?>(null);
        }

        return _repository.GetByIdAsync(_activeFolderId.Value, cancellationToken);
    }

    public Task SetActiveAsync(Guid? folderId, CancellationToken cancellationToken = default)
    {
        _activeFolderId = folderId;
        return Task.CompletedTask;
    }
}
