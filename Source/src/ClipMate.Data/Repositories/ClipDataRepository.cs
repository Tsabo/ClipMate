using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ClipMate.Data.Repositories;

/// <summary>
/// Repository for managing ClipData entities (clipboard format metadata).
/// </summary>
public class ClipDataRepository : IClipDataRepository
{
    private readonly ClipMateDbContext _context;

    public ClipDataRepository(ClipMateDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<ClipData>> GetByClipIdAsync(Guid clipId, CancellationToken cancellationToken = default)
    {
        return await _context.ClipData
            .Where(p => p.ClipId == clipId)
            .OrderBy(p => p.Format)
            .ToListAsync(cancellationToken);
    }

    public async Task<ClipData?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ClipData
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<ClipData> CreateAsync(ClipData clipData, CancellationToken cancellationToken = default)
    {
        _context.ClipData.Add(clipData);
        await _context.SaveChangesAsync(cancellationToken);
        return clipData;
    }

    public async Task CreateRangeAsync(IEnumerable<ClipData> clipDataList, CancellationToken cancellationToken = default)
    {
        await _context.ClipData.AddRangeAsync(clipDataList, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> DeleteByClipIdAsync(Guid clipId, CancellationToken cancellationToken = default)
    {
        var clipDataEntries = await _context.ClipData
            .Where(p => p.ClipId == clipId)
            .ToListAsync(cancellationToken);

        _context.ClipData.RemoveRange(clipDataEntries);
        await _context.SaveChangesAsync(cancellationToken);

        return clipDataEntries.Count;
    }
}
