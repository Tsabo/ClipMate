using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ClipMate.Data.Repositories;

/// <summary>
/// Repository for managing BLOB storage across all BLOB tables.
/// Provides unified access to BlobTxt, BlobJpg, BlobPng, and BlobBlob.
/// </summary>
public class BlobRepository : IBlobRepository
{
    private readonly ClipMateDbContext _context;

    public BlobRepository(ClipMateDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    // ==================== Create Operations ====================

    public async Task<BlobTxt> CreateTextAsync(BlobTxt blobTxt, CancellationToken cancellationToken = default)
    {
        _context.BlobTxt.Add(blobTxt);
        await _context.SaveChangesAsync(cancellationToken);
        return blobTxt;
    }

    public async Task<BlobJpg> CreateJpgAsync(BlobJpg blobJpg, CancellationToken cancellationToken = default)
    {
        _context.BlobJpg.Add(blobJpg);
        await _context.SaveChangesAsync(cancellationToken);
        return blobJpg;
    }

    public async Task<BlobPng> CreatePngAsync(BlobPng blobPng, CancellationToken cancellationToken = default)
    {
        _context.BlobPng.Add(blobPng);
        await _context.SaveChangesAsync(cancellationToken);
        return blobPng;
    }

    public async Task<BlobBlob> CreateBlobAsync(BlobBlob blobBlob, CancellationToken cancellationToken = default)
    {
        _context.BlobBlob.Add(blobBlob);
        await _context.SaveChangesAsync(cancellationToken);
        return blobBlob;
    }

    // ==================== Read Operations ====================

    public async Task<IReadOnlyList<BlobTxt>> GetTextByClipIdAsync(Guid clipId, CancellationToken cancellationToken = default)
    {
        return await _context.BlobTxt
            .Where(b => b.ClipId == clipId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BlobJpg>> GetJpgByClipIdAsync(Guid clipId, CancellationToken cancellationToken = default)
    {
        return await _context.BlobJpg
            .Where(b => b.ClipId == clipId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BlobPng>> GetPngByClipIdAsync(Guid clipId, CancellationToken cancellationToken = default)
    {
        return await _context.BlobPng
            .Where(b => b.ClipId == clipId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BlobBlob>> GetBlobByClipIdAsync(Guid clipId, CancellationToken cancellationToken = default)
    {
        return await _context.BlobBlob
            .Where(b => b.ClipId == clipId)
            .ToListAsync(cancellationToken);
    }

    // ==================== Delete Operations ====================

    public async Task<int> DeleteByClipIdAsync(Guid clipId, CancellationToken cancellationToken = default)
    {
        var totalDeleted = 0;

        // Delete from BlobTxt
        var textBlobs = await _context.BlobTxt
            .Where(b => b.ClipId == clipId)
            .ToListAsync(cancellationToken);
        _context.BlobTxt.RemoveRange(textBlobs);
        totalDeleted += textBlobs.Count;

        // Delete from BlobJpg
        var jpgBlobs = await _context.BlobJpg
            .Where(b => b.ClipId == clipId)
            .ToListAsync(cancellationToken);
        _context.BlobJpg.RemoveRange(jpgBlobs);
        totalDeleted += jpgBlobs.Count;

        // Delete from BlobPng
        var pngBlobs = await _context.BlobPng
            .Where(b => b.ClipId == clipId)
            .ToListAsync(cancellationToken);
        _context.BlobPng.RemoveRange(pngBlobs);
        totalDeleted += pngBlobs.Count;

        // Delete from BlobBlob
        var binaryBlobs = await _context.BlobBlob
            .Where(b => b.ClipId == clipId)
            .ToListAsync(cancellationToken);
        _context.BlobBlob.RemoveRange(binaryBlobs);
        totalDeleted += binaryBlobs.Count;

        await _context.SaveChangesAsync(cancellationToken);

        return totalDeleted;
    }
}
