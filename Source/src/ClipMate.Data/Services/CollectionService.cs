using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using Microsoft.EntityFrameworkCore;
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
    private string? _activeDatabaseKey;

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
            ModifiedAt = DateTime.UtcNow,
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

        if (_activeDatabaseKey == null)
            throw new InvalidOperationException("Active collection database key is not set.");

        using var scope = _serviceProvider.CreateScope();
        var databaseManager = scope.ServiceProvider.GetRequiredService<DatabaseManager>();
        var dbContext = databaseManager.GetDatabaseContext(_activeDatabaseKey);

        if (dbContext == null)
            throw new InvalidOperationException($"Database context for key '{_activeDatabaseKey}' not found.");

        var collection = await dbContext.Collections.FirstOrDefaultAsync(c => c.Id == _activeCollectionId.Value, cancellationToken);

        if (collection == null)
            throw new InvalidOperationException($"Active collection {_activeCollectionId} not found in database '{_activeDatabaseKey}'.");

        return collection;
    }

    public string? GetActiveDatabaseKey() => _activeDatabaseKey;

    public async Task<Collection?> GetFirstAcceptingCollectionAsync(CancellationToken cancellationToken = default)
    {
        if (_activeDatabaseKey == null)
            return null;

        using var scope = _serviceProvider.CreateScope();
        var databaseManager = scope.ServiceProvider.GetRequiredService<DatabaseManager>();
        var dbContext = databaseManager.GetDatabaseContext(_activeDatabaseKey);

        if (dbContext == null)
            return null;

        // Find first non-virtual collection that accepts new clips, ordered by SortKey
        return await dbContext.Collections
            .Where(c => !c.IsVirtual && c.AcceptNewClips && !c.ReadOnly)
            .OrderBy(c => c.SortKey)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SetActiveAsync(Guid id, string databaseKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseKey);

        // Verify collection exists in the specified database
        using var scope = _serviceProvider.CreateScope();
        var databaseManager = scope.ServiceProvider.GetRequiredService<DatabaseManager>();
        var dbContext = databaseManager.GetDatabaseContext(databaseKey);

        if (dbContext == null)
            throw new ArgumentException($"Database context for key '{databaseKey}' not found.", nameof(databaseKey));

        var collection = await dbContext.Collections.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (collection == null)
            throw new ArgumentException($"Collection {id} not found in database '{databaseKey}'.", nameof(id));

        _activeCollectionId = id;
        _activeDatabaseKey = databaseKey;
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
        {
            _activeCollectionId = null;
            _activeDatabaseKey = null;
        }

        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICollectionRepository>();
        var deleted = await repository.DeleteAsync(id, cancellationToken);

        if (!deleted)
            throw new InvalidOperationException($"Failed to delete collection {id}.");
    }
}
