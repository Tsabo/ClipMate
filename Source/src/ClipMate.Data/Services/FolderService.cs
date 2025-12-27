using ClipMate.Core.Models;
using ClipMate.Core.Services;

namespace ClipMate.Data.Services;

/// <summary>
/// Service for managing folders within collections.
/// </summary>
public class FolderService : IFolderService
{
    private readonly ICollectionService _collectionService;
    private readonly IDatabaseContextFactory _contextFactory;
    private Guid? _activeFolderId;

    public FolderService(IDatabaseContextFactory contextFactory,
        ICollectionService collectionService)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
    }

    public async Task<Folder> CreateAsync(string name, Guid collectionId, Guid? parentFolderId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var databaseKey = _collectionService.GetActiveDatabaseKey();
        if (string.IsNullOrEmpty(databaseKey))
            throw new InvalidOperationException("No active database selected");

        var repository = _contextFactory.GetFolderRepository(databaseKey);

        var folder = new Folder
        {
            Id = Guid.NewGuid(),
            Name = name,
            CollectionId = collectionId,
            ParentFolderId = parentFolderId,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
        };

        return await repository.CreateAsync(folder, cancellationToken);
    }

    public async Task<Folder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var databaseKey = _collectionService.GetActiveDatabaseKey();
        if (string.IsNullOrEmpty(databaseKey))
            return null;

        var repository = _contextFactory.GetFolderRepository(databaseKey);
        return await repository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<Folder>> GetByCollectionAsync(Guid collectionId, CancellationToken cancellationToken = default)
    {
        var databaseKey = _collectionService.GetActiveDatabaseKey();
        if (string.IsNullOrEmpty(databaseKey))
            return Array.Empty<Folder>();

        var repository = _contextFactory.GetFolderRepository(databaseKey);
        return await repository.GetByCollectionAsync(collectionId, cancellationToken);
    }

    public async Task<IReadOnlyList<Folder>> GetRootFoldersAsync(Guid collectionId, CancellationToken cancellationToken = default)
    {
        var databaseKey = _collectionService.GetActiveDatabaseKey();
        if (string.IsNullOrEmpty(databaseKey))
            return Array.Empty<Folder>();

        var repository = _contextFactory.GetFolderRepository(databaseKey);
        return await repository.GetRootFoldersAsync(collectionId, cancellationToken);
    }

    public async Task<IReadOnlyList<Folder>> GetChildFoldersAsync(Guid parentFolderId, CancellationToken cancellationToken = default)
    {
        var databaseKey = _collectionService.GetActiveDatabaseKey();
        if (string.IsNullOrEmpty(databaseKey))
            return Array.Empty<Folder>();

        var repository = _contextFactory.GetFolderRepository(databaseKey);
        return await repository.GetChildFoldersAsync(parentFolderId, cancellationToken);
    }

    public async Task UpdateAsync(Folder folder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(folder);

        var databaseKey = _collectionService.GetActiveDatabaseKey();
        if (string.IsNullOrEmpty(databaseKey))
            throw new InvalidOperationException("No active database selected");

        var repository = _contextFactory.GetFolderRepository(databaseKey);
        folder.ModifiedAt = DateTime.UtcNow;
        var updated = await repository.UpdateAsync(folder, cancellationToken);

        if (!updated)
            throw new InvalidOperationException($"Failed to update folder {folder.Id}.");
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var databaseKey = _collectionService.GetActiveDatabaseKey();
        if (string.IsNullOrEmpty(databaseKey))
            throw new InvalidOperationException("No active database selected");

        var repository = _contextFactory.GetFolderRepository(databaseKey);
        var deleted = await repository.DeleteAsync(id, cancellationToken);

        if (!deleted)
            throw new InvalidOperationException($"Failed to delete folder {id}.");
    }

    public async Task<Folder?> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        if (_activeFolderId == null)
            return null;

        var databaseKey = _collectionService.GetActiveDatabaseKey();
        if (string.IsNullOrEmpty(databaseKey))
            return null;

        var repository = _contextFactory.GetFolderRepository(databaseKey);
        return await repository.GetByIdAsync(_activeFolderId.Value, cancellationToken);
    }

    public Task SetActiveAsync(Guid? folderId, CancellationToken cancellationToken = default)
    {
        _activeFolderId = folderId;

        return Task.CompletedTask;
    }
}
