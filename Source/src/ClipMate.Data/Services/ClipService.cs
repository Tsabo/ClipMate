using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;

namespace ClipMate.Data.Services;

/// <summary>
/// Service for managing clips (CRUD operations, history management).
/// </summary>
public class ClipService : IClipService
{
    private readonly IClipRepository _repository;

    /// <inheritdoc/>
    public event EventHandler<Clip>? ClipAdded;

    public ClipService(IClipRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <inheritdoc/>
    public async Task<Clip?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _repository.GetByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Clip>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        return await _repository.GetRecentAsync(count, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Clip>> GetByCollectionAsync(Guid collectionId, CancellationToken cancellationToken = default)
    {
        return await _repository.GetByCollectionAsync(collectionId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Clip>> GetByFolderAsync(Guid folderId, CancellationToken cancellationToken = default)
    {
        return await _repository.GetByFolderAsync(folderId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Clip>> GetFavoritesAsync(CancellationToken cancellationToken = default)
    {
        return await _repository.GetFavoritesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Clip> CreateAsync(Clip clip, CancellationToken cancellationToken = default)
    {
        if (clip == null)
        {
            throw new ArgumentNullException(nameof(clip));
        }

        // Check for duplicate by content hash
        var existing = await _repository.GetByContentHashAsync(clip.ContentHash, cancellationToken);
        if (existing != null)
        {
            // Return existing clip instead of creating duplicate
            return existing;
        }

        var createdClip = await _repository.CreateAsync(clip, cancellationToken);
        
        // Raise the ClipAdded event
        ClipAdded?.Invoke(this, createdClip);
        
        return createdClip;
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(Clip clip, CancellationToken cancellationToken = default)
    {
        if (clip == null)
        {
            throw new ArgumentNullException(nameof(clip));
        }

        await _repository.UpdateAsync(clip, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAsync(id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> DeleteOlderThanAsync(DateTime olderThan, CancellationToken cancellationToken = default)
    {
        // Get all clips older than the specified date
        var oldClips = await _repository.SearchAsync(string.Empty, cancellationToken);
        var clipsToDelete = oldClips.Where(c => c.CapturedAt < olderThan).ToList();

        // Delete each clip
        foreach (var clip in clipsToDelete)
        {
            await _repository.DeleteAsync(clip.Id, cancellationToken);
        }

        return clipsToDelete.Count;
    }

    /// <inheritdoc/>
    public async Task<bool> IsDuplicateAsync(string contentHash, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentHash))
        {
            throw new ArgumentException("Content hash cannot be null or empty.", nameof(contentHash));
        }

        var existing = await _repository.GetByContentHashAsync(contentHash, cancellationToken);
        return existing != null;
    }
}
