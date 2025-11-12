using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ClipMate.Data.Repositories;

/// <summary>
/// Entity Framework Core implementation of the folder repository.
/// </summary>
public class FolderRepository : IFolderRepository
{
    private readonly ClipMateDbContext _context;

    public FolderRepository(ClipMateDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Folder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Folders
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Folder>> GetByCollectionAsync(Guid collectionId, CancellationToken cancellationToken = default)
    {
        return await _context.Folders
            .Where(p => p.CollectionId == collectionId)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Folder>> GetChildFoldersAsync(Guid parentFolderId, CancellationToken cancellationToken = default)
    {
        return await _context.Folders
            .Where(p => p.ParentFolderId == parentFolderId)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Folder>> GetRootFoldersAsync(Guid collectionId, CancellationToken cancellationToken = default)
    {
        return await _context.Folders
            .Where(p => p.CollectionId == collectionId && p.ParentFolderId == null)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Folder> CreateAsync(Folder folder, CancellationToken cancellationToken = default)
    {
        if (folder == null)
        {
            throw new ArgumentNullException(nameof(folder));
        }

        _context.Folders.Add(folder);
        await _context.SaveChangesAsync(cancellationToken);
        return folder;
    }

    public async Task<bool> UpdateAsync(Folder folder, CancellationToken cancellationToken = default)
    {
        if (folder == null)
        {
            throw new ArgumentNullException(nameof(folder));
        }

        _context.Folders.Update(folder);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var folder = await _context.Folders.FindAsync(new object[] { id }, cancellationToken);
        if (folder == null)
        {
            return false;
        }

        _context.Folders.Remove(folder);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
