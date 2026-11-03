using ClipMate.Core.Models;
using ClipMate.Core.Services.Abstractions;
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

        foreach (var (_, context) in _databaseManager.CreateAllDatabaseContexts())
        {
            await using (context)
            {
                var collections = await context.Collections.ToListAsync(cancellationToken);
                allCollections.AddRange(collections);
            }
        }

        return allCollections;
    }

    public async Task<IReadOnlyList<Collection>> GetAllByDatabaseKeyAsync(string databaseKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseKey);

        var repository = _contextFactory.GetCollectionRepository(databaseKey);
        return await repository.GetAllAsync(cancellationToken);
    }

    public async Task<Collection> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        if (_activeCollectionId == null)
            throw new InvalidOperationException("No active collection set.");

        if (_activeDatabaseKey == null)
            throw new InvalidOperationException("Active collection database key is not set.");

        // Use contextFactory which handles both config keys and file paths
        var repository = _contextFactory.GetCollectionRepository(_activeDatabaseKey);
        var collection = await repository.GetByIdAsync(_activeCollectionId.Value, cancellationToken);

        return collection ?? throw new InvalidOperationException($"Active collection {_activeCollectionId} not found in database '{_activeDatabaseKey}'.");
    }

    public string? GetActiveDatabaseKey() => _activeDatabaseKey;

    public async Task<Collection?> GetFirstAcceptingCollectionAsync(CancellationToken cancellationToken = default)
    {
        if (_activeDatabaseKey == null)
            return null;

        // Use contextFactory which handles both config keys and file paths
        var repository = _contextFactory.GetCollectionRepository(_activeDatabaseKey);
        var collections = await repository.GetAllAsync(cancellationToken);

        // Find first non-virtual collection that accepts new clips, ordered by SortKey
        return collections
            .Where(p =>
                !(p.LmType == CollectionLmType.Virtual || p.ListType == CollectionListType.Smart || p.ListType == CollectionListType.SqlBased) // not virtual
                && p is { AcceptNewClips: true, ReadOnly: false })
            .OrderBy(p => p.SortKey)
            .FirstOrDefault();
    }

    public async Task SetActiveAsync(Guid id, string databaseKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseKey);

        // Use contextFactory which handles both config keys and file paths
        var repository = _contextFactory.GetCollectionRepository(databaseKey);
        var collection = await repository.GetByIdAsync(id, cancellationToken);

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

        await using var dbContext = _databaseManager.CreateDatabaseContext(databaseKey)
                                    ?? throw new InvalidOperationException($"Database context for '{databaseKey}' not found");

        // First check if this is a virtual collection
        var collection = await dbContext.Collections.FindAsync([id], cancellationToken);
        if (collection == null)
            throw new InvalidOperationException($"Collection {id} not found.");

        // Always check for and remove any clips or child collections referencing this collection
        // Even virtual collections might have orphaned clips due to bugs
        var clips = await dbContext.Clips
            .Where(p => p.CollectionId == id)
            .ToListAsync(cancellationToken);

        if (clips.Count > 0)
            dbContext.Clips.RemoveRange(clips);

        // Delete all child collections/folders (folders are stored as Collections with ParentId)
        var childCollections = await dbContext.Collections
            .Where(p => p.ParentId == id)
            .ToListAsync(cancellationToken);

        if (childCollections.Count > 0)
            dbContext.Collections.RemoveRange(childCollections);

        if (clips.Count > 0 || childCollections.Count > 0)
            await dbContext.SaveChangesAsync(cancellationToken);

        // Now delete the collection itself
        var repository = _contextFactory.GetCollectionRepository(databaseKey);
        var deleted = await repository.DeleteAsync(id, cancellationToken);

        if (!deleted)
            throw new InvalidOperationException($"Failed to delete collection {id}.");
    }

    public async Task<int> GetCollectionItemCountAsync(Guid collectionId, string databaseKey, CancellationToken cancellationToken = default)
    {
        await using var dbContext = _databaseManager.CreateDatabaseContext(databaseKey)
                                    ?? throw new InvalidOperationException($"Database context for '{databaseKey}' not found");

        return await dbContext.Clips
            .CountAsync(p => p.CollectionId == collectionId && !p.Del, cancellationToken);
    }

    public async Task<bool> MoveCollectionUpAsync(Guid collectionId, string databaseKey, CancellationToken cancellationToken = default) =>
        await MoveCollectionAsync(collectionId, -1, databaseKey, cancellationToken);

    public async Task<bool> MoveCollectionDownAsync(Guid collectionId, string databaseKey, CancellationToken cancellationToken = default) =>
        await MoveCollectionAsync(collectionId, 1, databaseKey, cancellationToken);

    public async Task ReorderCollectionsAsync(List<Guid> droppedCollectionIds,
        Guid targetCollectionId,
        bool insertAfter,
        string databaseKey,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = _databaseManager.CreateDatabaseContext(databaseKey)
                                    ?? throw new InvalidOperationException($"Database context for '{databaseKey}' not found");

        var siblings = await GetTopLevelCollectionsAsync(dbContext, cancellationToken);

        // Remove dropped collections from current positions
        var droppedCollections = siblings.Where(p => droppedCollectionIds.Contains(p.Id)).ToList();
        foreach (var item in droppedCollections)
            siblings.Remove(item);

        // Find target collection index
        var targetIndex = siblings.FindIndex(p => p.Id == targetCollectionId);
        if (targetIndex < 0)
            throw new InvalidOperationException($"Target collection {targetCollectionId} not found");

        // Insert dropped collections at new position
        var insertIndex = insertAfter
            ? targetIndex + 1
            : targetIndex;

        siblings.InsertRange(insertIndex, droppedCollections);
        Renumber(siblings);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Swaps a collection's place with the sibling <paramref name="offset" /> positions away, as the tree shows them.
    /// </summary>
    private async Task<bool> MoveCollectionAsync(Guid collectionId, int offset, string databaseKey, CancellationToken cancellationToken)
    {
        await using var dbContext = _databaseManager.CreateDatabaseContext(databaseKey)
                                    ?? throw new InvalidOperationException($"Database context for '{databaseKey}' not found");

        var siblings = await GetTopLevelCollectionsAsync(dbContext, cancellationToken);
        var currentIndex = siblings.FindIndex(p => p.Id == collectionId);
        var otherIndex = currentIndex + offset;
        if (currentIndex < 0 || otherIndex < 0 || otherIndex >= siblings.Count) // At the end already, or not found
            return false;

        var currentCollection = siblings[currentIndex];
        var otherCollection = siblings[otherIndex];

        // Swapping equal keys would change nothing, so spread the keys out first
        if (currentCollection.SortKey == otherCollection.SortKey)
            Renumber(siblings);

        (otherCollection.SortKey, currentCollection.SortKey) = (currentCollection.SortKey, otherCollection.SortKey);

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// The collections the tree shows directly under the Database, in the order it shows them.
    /// </summary>
    private static async Task<List<Collection>> GetTopLevelCollectionsAsync(ClipMateDbContext dbContext, CancellationToken cancellationToken)
    {
        var collections = await dbContext.Collections.ToListAsync(cancellationToken);
        var topLevelIds = CollectionEntry.TopLevel(collections.Select(CollectionEntry.From).ToList())
            .Select(p => p.Id)
            .ToHashSet();

        return collections.Where(p => topLevelIds.Contains(p.Id))
            .OrderBy(p => p.SortKey)
            .ThenBy(p => p.Title, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Gives <paramref name="collections" /> sort keys 100, 200, 300... in their current order, as ClipMate 7 spaces them.
    /// </summary>
    private static void Renumber(List<Collection> collections)
    {
        for (var i = 0; i < collections.Count; i++)
            collections[i].SortKey = (i + 1) * 100;
    }

    public async Task<int> ResequenceSortKeysAsync(string databaseKey, CancellationToken cancellationToken = default)
    {
        await using var dbContext = _databaseManager.CreateDatabaseContext(databaseKey)
                                    ?? throw new InvalidOperationException($"Database context for '{databaseKey}' not found");

        // Get all non-virtual collections ordered by current SortKey
        var collections = await dbContext.Collections
            .Where(Collection.NotVirtual)
            .OrderBy(p => p.SortKey)
            .ToListAsync(cancellationToken);

        // Reassign SortKey values as 10, 20, 30, etc.
        for (var i = 0; i < collections.Count; i++)
            collections[i].SortKey = (i + 1) * 10;

        await dbContext.SaveChangesAsync(cancellationToken);
        return collections.Count;
    }

    public async Task<Collection?> GetFavoriteCollectionAsync(string databaseKey, CancellationToken cancellationToken = default)
    {
        await using var dbContext = _databaseManager.CreateDatabaseContext(databaseKey)
                                    ?? throw new InvalidOperationException($"Database context for '{databaseKey}' not found");

        return await dbContext.Collections
            .Where(Collection.NotVirtual)
            .Where(p => p.Favorite)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Collection> CreateAsync(string name, Guid? parentId, string databaseKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        await using var dbContext = _databaseManager.CreateDatabaseContext(databaseKey)
                                    ?? throw new InvalidOperationException($"Database context for '{databaseKey}' not found");

        // Get the max SortKey to place new collection at the end
        var maxSortKey = await dbContext.Collections
            .Where(Collection.NotVirtual)
            .Where(p => p.ParentId == parentId)
            .MaxAsync(p => (int?)p.SortKey, cancellationToken) ?? 0;

        var collection = new Collection
        {
            Id = Guid.NewGuid(),
            Name = name,
            ParentId = parentId,
            ParentGuid = parentId,
            SortKey = maxSortKey + 10,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            AcceptNewClips = true,
            AcceptDuplicates = true,
            LmType = CollectionLmType.Normal,
            ListType = CollectionListType.Normal,
        };

        dbContext.Collections.Add(collection);
        await dbContext.SaveChangesAsync(cancellationToken);
        return collection;
    }

    public async Task<Collection?> GetByNameAsync(string name, string databaseKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        await using var dbContext = _databaseManager.CreateDatabaseContext(databaseKey)
                                    ?? throw new InvalidOperationException($"Database context for '{databaseKey}' not found");

        return await dbContext.Collections
            .Where(Collection.NotVirtual)
            .Where(p => p.Name == name)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
