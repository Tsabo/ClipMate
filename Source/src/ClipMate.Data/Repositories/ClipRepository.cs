using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ClipMate.Data.Repositories;

/// <summary>
/// Entity Framework Core implementation of the clip repository.
/// </summary>
public class ClipRepository : IClipRepository
{
    private readonly ClipMateDbContext _context;

    public ClipRepository(ClipMateDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Clip?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Clips
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Clip>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Clips
            .OrderByDescending(p => p.CapturedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Clip>> GetByTypeAsync(ClipType type, CancellationToken cancellationToken = default)
    {
        return await _context.Clips
            .Where(p => p.Type == type)
            .OrderByDescending(p => p.CapturedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Clip>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        return await _context.Clips
            .OrderByDescending(p => p.CapturedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Clip>> GetByCollectionAsync(Guid collectionId, CancellationToken cancellationToken = default)
    {
        return await _context.Clips
            .Where(p => p.CollectionId == collectionId)
            .OrderByDescending(p => p.CapturedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Clip>> GetByFolderAsync(Guid folderId, CancellationToken cancellationToken = default)
    {
        return await _context.Clips
            .Where(p => p.FolderId == folderId)
            .OrderByDescending(p => p.CapturedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Clip>> GetFavoritesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Clips
            .Where(p => p.IsFavorite)
            .OrderByDescending(p => p.CapturedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Clip>> GetPinnedAsync(CancellationToken cancellationToken = default)
    {
        // Note: Clip model doesn't have IsPinned property yet
        // Return empty list for now, or filter by Label == "Pinned" if that's the approach
        return await _context.Clips
            .Where(p => p.Label == "Pinned")
            .OrderByDescending(p => p.CapturedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Clip?> GetByContentHashAsync(string contentHash, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentHash))
        {
            return null;
        }

        return await _context.Clips
            .FirstOrDefaultAsync(p => p.ContentHash == contentHash, cancellationToken);
    }

    public async Task<IReadOnlyList<Clip>> SearchAsync(string searchText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return new List<Clip>();
        }

        return await _context.Clips
            .Where(p => EF.Functions.Like(p.TextContent ?? "", $"%{searchText}%"))
            .OrderByDescending(p => p.CapturedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Clip> CreateAsync(Clip clip, CancellationToken cancellationToken = default)
    {
        if (clip == null)
        {
            throw new ArgumentNullException(nameof(clip));
        }

        _context.Clips.Add(clip);
        await _context.SaveChangesAsync(cancellationToken);
        return clip;
    }

    public async Task<Clip> AddAsync(Clip clip, CancellationToken cancellationToken = default)
    {
        return await CreateAsync(clip, cancellationToken);
    }

    public async Task<bool> UpdateAsync(Clip clip, CancellationToken cancellationToken = default)
    {
        if (clip == null)
        {
            throw new ArgumentNullException(nameof(clip));
        }

        _context.Clips.Update(clip);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var clip = await _context.Clips.FindAsync(new object[] { id }, cancellationToken);
        if (clip == null)
        {
            return false;
        }

        _context.Clips.Remove(clip);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> DeleteOlderThanAsync(DateTime cutoffDate, CancellationToken cancellationToken = default)
    {
        var clipsToDelete = await _context.Clips
            .Where(p => p.CapturedAt < cutoffDate && !p.IsFavorite)
            .ToListAsync(cancellationToken);

        _context.Clips.RemoveRange(clipsToDelete);
        await _context.SaveChangesAsync(cancellationToken);
        
        return clipsToDelete.Count;
    }

    public async Task<long> GetCountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Clips.LongCountAsync(cancellationToken);
    }
}
