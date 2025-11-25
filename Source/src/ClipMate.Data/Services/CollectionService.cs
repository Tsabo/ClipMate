using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;

namespace ClipMate.Data.Services;

/// <summary>
/// Service for managing collections.
/// </summary>
public class CollectionService : ICollectionService
{
    private readonly ICollectionRepository _repository;
    private Guid? _activeCollectionId;

    public CollectionService(ICollectionRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<Collection> CreateAsync(string name, string? description = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var collection = new Collection
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };

        return await _repository.CreateAsync(collection, cancellationToken);
    }

    public async Task<Collection?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _repository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<Collection>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _repository.GetAllAsync(cancellationToken);
    }

    public async Task<Collection> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        if (_activeCollectionId == null)
        {
            throw new InvalidOperationException("No active collection set.");
        }

        var collection = await _repository.GetByIdAsync(_activeCollectionId.Value, cancellationToken);
        if (collection == null)
        {
            throw new InvalidOperationException($"Active collection {_activeCollectionId} not found.");
        }

        return collection;
    }

    public async Task SetActiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Verify collection exists
        var collection = await _repository.GetByIdAsync(id, cancellationToken);
        if (collection == null)
        {
            throw new ArgumentException($"Collection {id} not found.", nameof(id));
        }

        _activeCollectionId = id;
    }

    public async Task UpdateAsync(Collection collection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collection);
        collection.ModifiedAt = DateTime.UtcNow;
        var updated = await _repository.UpdateAsync(collection, cancellationToken);
        if (!updated)
        {
            throw new InvalidOperationException($"Failed to update collection {collection.Id}.");
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (_activeCollectionId == id)
        {
            _activeCollectionId = null;
        }
        
        var deleted = await _repository.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            throw new InvalidOperationException($"Failed to delete collection {id}.");
        }
    }
}
