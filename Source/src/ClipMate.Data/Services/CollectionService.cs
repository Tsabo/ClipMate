using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ClipMate.Data.Services;

/// <summary>
/// Service for managing collections.
/// Registered as singleton to maintain in-memory active collection state across the application.
/// </summary>
public class CollectionService : ICollectionService
{
    private readonly IServiceProvider _serviceProvider;
    private Guid? _activeCollectionId;

    public CollectionService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
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

        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICollectionRepository>();

        return await repository.CreateAsync(collection, cancellationToken);
    }

    public async Task<Collection?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICollectionRepository>();

        return await repository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<Collection>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICollectionRepository>();

        return await repository.GetAllAsync(cancellationToken);
    }

    public async Task<Collection> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        if (_activeCollectionId == null)
            throw new InvalidOperationException("No active collection set.");

        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICollectionRepository>();
        var collection = await repository.GetByIdAsync(_activeCollectionId.Value, cancellationToken);

        if (collection == null)
            throw new InvalidOperationException($"Active collection {_activeCollectionId} not found.");

        return collection;
    }

    public async Task SetActiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Verify collection exists
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICollectionRepository>();
        var collection = await repository.GetByIdAsync(id, cancellationToken);

        if (collection == null)
            throw new ArgumentException($"Collection {id} not found.", nameof(id));

        _activeCollectionId = id;
    }

    public async Task UpdateAsync(Collection collection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collection);
        collection.ModifiedAt = DateTime.UtcNow;

        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICollectionRepository>();
        var updated = await repository.UpdateAsync(collection, cancellationToken);

        if (!updated)
            throw new InvalidOperationException($"Failed to update collection {collection.Id}.");
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (_activeCollectionId == id)
            _activeCollectionId = null;

        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICollectionRepository>();
        var deleted = await repository.DeleteAsync(id, cancellationToken);

        if (!deleted)
            throw new InvalidOperationException($"Failed to delete collection {id}.");
    }
}
