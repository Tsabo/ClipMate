using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ClipMate.Data.Repositories;

/// <summary>
/// Repository for folder operations.
/// In ClipMate 7.5, folders are stored in the COLL table with LmType=2.
/// </summary>
public class FolderRepository : IFolderRepository
{
    private readonly ClipMateDbContext _context;

    public FolderRepository(ClipMateDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<Folder>> GetByCollectionAsync(Guid collectionId, CancellationToken cancellationToken = default)
    {
        // In ClipMate 7.5, folders are Collections with LmType = 2
        var collections = await _context.Collections
            .Where(c => c.ParentId == collectionId && c.LmType == 2)
            .OrderBy(c => c.SortKey)
            .ToListAsync(cancellationToken);

        return collections.Select(ConvertToFolder).ToList();
    }

    public async Task<IReadOnlyList<Folder>> GetChildFoldersAsync(Guid parentFolderId, CancellationToken cancellationToken = default)
    {
        var collections = await _context.Collections
            .Where(c => c.ParentId == parentFolderId && c.LmType == 2)
            .OrderBy(c => c.SortKey)
            .ToListAsync(cancellationToken);

        return collections.Select(ConvertToFolder).ToList();
    }

    public async Task<IReadOnlyList<Folder>> GetRootFoldersAsync(Guid collectionId, CancellationToken cancellationToken = default)
    {
        var collections = await _context.Collections
            .Where(c => c.ParentId == collectionId && c.LmType == 2 && c.ParentId != null)
            .OrderBy(c => c.SortKey)
            .ToListAsync(cancellationToken);

        return collections.Select(ConvertToFolder).ToList();
    }

    public async Task<Folder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var collection = await _context.Collections
            .FirstOrDefaultAsync(c => c.Id == id && c.LmType == 2, cancellationToken);

        return collection != null ? ConvertToFolder(collection) : null;
    }

    public async Task<Folder?> GetByNameAsync(Guid collectionId, string name, CancellationToken cancellationToken = default)
    {
        var collection = await _context.Collections
            .FirstOrDefaultAsync(
                c => c.ParentId == collectionId && 
                     c.Title == name && 
                     c.LmType == 2, 
                cancellationToken);

        return collection != null ? ConvertToFolder(collection) : null;
    }

    public async Task<Folder?> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var collection = await _context.Collections
            .FirstOrDefaultAsync(
                c => c.LmType == 2 && c.NewClipsGo == 1, 
                cancellationToken);

        return collection != null ? ConvertToFolder(collection) : null;
    }

    public async Task<Folder> CreateAsync(Folder folder, CancellationToken cancellationToken = default)
    {
        var collection = ConvertToCollection(folder);
        collection.LmType = 2; // Ensure it's marked as a folder
        collection.CreatedAt = DateTime.UtcNow;
        
        _context.Collections.Add(collection);
        await _context.SaveChangesAsync(cancellationToken);
        
        return ConvertToFolder(collection);
    }

    public async Task<bool> UpdateAsync(Folder folder, CancellationToken cancellationToken = default)
    {
        var collection = await _context.Collections.FindAsync(new object[] { folder.Id }, cancellationToken);
        if (collection == null)
            return false;

        UpdateCollectionFromFolder(collection, folder);
        _context.Collections.Update(collection);
        await _context.SaveChangesAsync(cancellationToken);
        
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var collection = await _context.Collections.FindAsync(new object[] { id }, cancellationToken);
        if (collection == null)
            return false;

        // Only allow deleting folders (LmType = 2), not regular collections
        if (collection.LmType != 2)
            throw new InvalidOperationException("Cannot delete a non-folder collection through FolderRepository");

        _context.Collections.Remove(collection);
        await _context.SaveChangesAsync(cancellationToken);
        
        return true;
    }

    public async Task SetActiveAsync(Guid? folderId, CancellationToken cancellationToken = default)
    {
        // Clear all active flags
        var allFolders = await _context.Collections
            .Where(c => c.LmType == 2)
            .ToListAsync(cancellationToken);

        foreach (var folder in allFolders)
        {
            folder.NewClipsGo = 0;
        }

        // Set new active folder if specified
        if (folderId.HasValue)
        {
            var activeFolder = allFolders.FirstOrDefault(f => f.Id == folderId.Value);
            if (activeFolder != null)
            {
                activeFolder.NewClipsGo = 1;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Converts a Collection (with LmType=2) to a Folder model.
    /// </summary>
    private static Folder ConvertToFolder(Collection collection)
    {
        return new Folder
        {
            Id = collection.Id,
            Name = collection.Title,
            CollectionId = collection.ParentId ?? Guid.Empty,
            ParentFolderId = collection.ParentId,
            SortOrder = collection.SortKey,
            CreatedAt = collection.CreatedAt,
            ModifiedAt = collection.LastUpdateTime,
            IsSystemFolder = false, // Could be derived from specific GUIDs
            FolderType = FolderType.Normal, // Default
            IconName = collection.IlIndex.ToString()
        };
    }

    /// <summary>
    /// Converts a Folder model to a Collection (with LmType=2).
    /// </summary>
    private static Collection ConvertToCollection(Folder folder)
    {
        return new Collection
        {
            Id = folder.Id,
            ParentId = folder.ParentFolderId ?? folder.CollectionId,
            ParentGuid = folder.ParentFolderId ?? folder.CollectionId,
            Title = folder.Name,
            LmType = 2, // Folder
            ListType = 0,
            SortKey = folder.SortOrder,
            IlIndex = int.TryParse(folder.IconName, out var iconIndex) ? iconIndex : 0,
            RetentionLimit = 0,
            NewClipsGo = 0,
            AcceptNewClips = true,
            ReadOnly = false,
            AcceptDuplicates = false,
            SortColumn = -2,
            SortAscending = false,
            Encrypted = false,
            Favorite = false,
            LastUserId = null,
            LastUpdateTime = folder.ModifiedAt,
            LastKnownCount = null,
            Sql = null,
            CreatedAt = folder.CreatedAt
        };
    }

    /// <summary>
    /// Updates a Collection from a Folder model.
    /// </summary>
    private static void UpdateCollectionFromFolder(Collection collection, Folder folder)
    {
        collection.Title = folder.Name;
        collection.ParentId = folder.ParentFolderId ?? folder.CollectionId;
        collection.ParentGuid = folder.ParentFolderId ?? folder.CollectionId;
        collection.SortKey = folder.SortOrder;
        collection.IlIndex = int.TryParse(folder.IconName, out var iconIndex) ? iconIndex : collection.IlIndex;
        collection.LastUpdateTime = DateTime.UtcNow;
    }
}
