using ClipMate.Core.Models;
using ClipMate.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace ClipMate.Data.Services;

/// <summary>
/// Service for managing collections.
/// Registered as singleton to maintain in-memory active collection state across the application.
/// </summary>
public class CollectionService : ICollectionService
{
    private readonly IDatabaseContextFactory _contextFactory;
    private readonly IDatabaseManager _databaseManager;
    private Guid? _activeCollectionId;
    private string? _activeDatabaseKey;

    public CollectionService(IDatabaseManager databaseManager, IDatabaseContextFactory contextFactory)
    {
        _databaseManager = databaseManager ?? throw new ArgumentNullException(nameof(databaseManager));
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task<Collection> CreateAsync(string name, string? description = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (_activeDatabaseKey == null)
            throw new InvalidOperationException("No active database set. Cannot create collection.");

        var collection = new Collection
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
        };

        var repository = _contextFactory.GetCollectionRepository(_activeDatabaseKey);
        return await repository.CreateAsync(collection, cancellationToken);
    }

    public async Task<Collection?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (_activeDatabaseKey == null)
            throw new InvalidOperationException("No active database set.");

        var repository = _contextFactory.GetCollectionRepository(_activeDatabaseKey);
        return await repository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<Collection>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var allCollections = new List<Collection>();

        foreach (var (dbKey, context) in _databaseManager.GetAllDatabaseContexts())
        {
            var collections = await context.Collections.ToListAsync(cancellationToken);
            allCollections.AddRange(collections);
        }

        return allCollections;
    }

    public async Task<Collection> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        if (_activeCollectionId == null)
            throw new InvalidOperationException("No active collection set.");

        if (_activeDatabaseKey == null)
            throw new InvalidOperationException("Active collection database key is not set.");

        var dbContext = _databaseManager.GetDatabaseContext(_activeDatabaseKey);

        if (dbContext == null)
            throw new InvalidOperationException($"Database context for key '{_activeDatabaseKey}' not found.");

        var collection = await dbContext.Collections.FirstOrDefaultAsync(p => p.Id == _activeCollectionId.Value, cancellationToken);

        return collection ?? throw new InvalidOperationException($"Active collection {_activeCollectionId} not found in database '{_activeDatabaseKey}'.");
    }

    public string? GetActiveDatabaseKey() => _activeDatabaseKey;

    public async Task<Collection?> GetFirstAcceptingCollectionAsync(CancellationToken cancellationToken = default)
    {
        if (_activeDatabaseKey == null)
            return null;

        var dbContext = _databaseManager.GetDatabaseContext(_activeDatabaseKey);

        if (dbContext == null)
            return null;

        // Find first non-virtual collection that accepts new clips, ordered by SortKey
        return await dbContext.Collections
            .Where(p =>
                !(p.LmType == CollectionLmType.Virtual || p.ListType == CollectionListType.Smart || p.ListType == CollectionListType.SqlBased) // not virtual
                && p.AcceptNewClips && !p.ReadOnly)
            .OrderBy(p => p.SortKey)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SetActiveAsync(Guid id, string databaseKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseKey);

        // Verify collection exists in the specified database
        var dbContext = _databaseManager.GetDatabaseContext(databaseKey);

        if (dbContext == null)
            throw new ArgumentException($"Database context for key '{databaseKey}' not found.", nameof(databaseKey));

        var collection = await dbContext.Collections.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (collection == null)
            throw new ArgumentException($"Collection {id} not found in database '{databaseKey}'.", nameof(id));

        _activeCollectionId = id;
        _activeDatabaseKey = databaseKey;
    }

    public async Task UpdateAsync(Collection collection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collection);

        if (_activeDatabaseKey == null)
            throw new InvalidOperationException("No active database set. Cannot update collection.");

        collection.ModifiedAt = DateTime.UtcNow;

        var repository = _contextFactory.GetCollectionRepository(_activeDatabaseKey);
        var updated = await repository.UpdateAsync(collection, cancellationToken);

        if (!updated)
            throw new InvalidOperationException($"Failed to update collection {collection.Id}.");
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (_activeDatabaseKey == null)
            throw new InvalidOperationException("No active database set. Cannot delete collection.");

        var databaseKey = _activeDatabaseKey; // Save before potentially clearing

        if (_activeCollectionId == id)
        {
            _activeCollectionId = null;
            _activeDatabaseKey = null;
        }

        var repository = _contextFactory.GetCollectionRepository(databaseKey);
        var deleted = await repository.DeleteAsync(id, cancellationToken);

        if (!deleted)
            throw new InvalidOperationException($"Failed to delete collection {id}.");
    }

    public async Task<int> GetCollectionItemCountAsync(Guid collectionId, string databaseKey, CancellationToken cancellationToken = default)
    {
        var dbContext = _databaseManager.GetDatabaseContext(databaseKey)
                        ?? throw new InvalidOperationException($"Database context for '{databaseKey}' not found");

        return await dbContext.Clips
            .CountAsync(p => p.CollectionId == collectionId && !p.Del, cancellationToken);
    }

    public async Task<bool> MoveCollectionUpAsync(Guid collectionId, string databaseKey, CancellationToken cancellationToken = default)
    {
        var dbContext = _databaseManager.GetDatabaseContext(databaseKey)
                        ?? throw new InvalidOperationException($"Database context for '{databaseKey}' not found");

        // Get all non-virtual collections ordered by SortKey
        var collections = await dbContext.Collections
            .Where(p => !p.IsVirtual)
            .OrderBy(p => p.SortKey)
            .ToListAsync(cancellationToken);

        var currentIndex = collections.FindIndex(p => p.Id == collectionId);
        if (currentIndex <= 0) // Already at top or not found
            return false;

        // Swap SortKey with previous collection
        var currentCollection = collections[currentIndex];
        var previousCollection = collections[currentIndex - 1];
        (previousCollection.SortKey, currentCollection.SortKey) = (currentCollection.SortKey, previousCollection.SortKey);

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> MoveCollectionDownAsync(Guid collectionId, string databaseKey, CancellationToken cancellationToken = default)
    {
        var dbContext = _databaseManager.GetDatabaseContext(databaseKey)
                        ?? throw new InvalidOperationException($"Database context for '{databaseKey}' not found");

        // Get all non-virtual collections ordered by SortKey
        var collections = await dbContext.Collections
            .Where(p => !p.IsVirtual)
            .OrderBy(p => p.SortKey)
            .ToListAsync(cancellationToken);

        var currentIndex = collections.FindIndex(p => p.Id == collectionId);
        if (currentIndex < 0 || currentIndex >= collections.Count - 1) // Already at bottom or not found
            return false;

        // Swap SortKey with next collection
        var currentCollection = collections[currentIndex];
        var nextCollection = collections[currentIndex + 1];
        (nextCollection.SortKey, currentCollection.SortKey) = (currentCollection.SortKey, nextCollection.SortKey);

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task ReorderCollectionsAsync(List<Guid> droppedCollectionIds,
        Guid targetCollectionId,
        bool insertAfter,
        string databaseKey,
        CancellationToken cancellationToken = default)
    {
        var dbContext = _databaseManager.GetDatabaseContext(databaseKey)
                        ?? throw new InvalidOperationException($"Database context for '{databaseKey}' not found");

        // Get all non-virtual collections ordered by SortKey
        var allCollections = await dbContext.Collections
            .Where(p => !p.IsVirtual)
            .OrderBy(p => p.SortKey)
            .ToListAsync(cancellationToken);

        // Remove dropped collections from current positions
        var droppedCollections = allCollections.Where(p => droppedCollectionIds.Contains(p.Id)).ToList();
        foreach (var item in droppedCollections)
            allCollections.Remove(item);

        // Find target collection index
        var targetIndex = allCollections.FindIndex(p => p.Id == targetCollectionId);
        if (targetIndex < 0)
            throw new InvalidOperationException($"Target collection {targetCollectionId} not found");

        // Insert dropped collections at new position
        var insertIndex = insertAfter
            ? targetIndex + 1
            : targetIndex;

        allCollections.InsertRange(insertIndex, droppedCollections);

        // Reassign SortKey values based on new order
        for (var i = 0; i < allCollections.Count; i++)
            allCollections[i].SortKey = i;

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
