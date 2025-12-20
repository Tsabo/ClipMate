using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using ClipMate.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Service for managing clips (CRUD operations, history management).
/// Registered as singleton to support multi-database operations via IClipRepositoryFactory.
/// </summary>
public class ClipService : IClipService
{
    private readonly ILogger<ClipService> _logger;
    private readonly IClipRepositoryFactory _repositoryFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly ISoundService _soundService;

    public ClipService(IClipRepositoryFactory repositoryFactory,
        IServiceProvider serviceProvider,
        ISoundService soundService,
        ILogger<ClipService> logger)
    {
        _repositoryFactory = repositoryFactory ?? throw new ArgumentNullException(nameof(repositoryFactory));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _soundService = soundService ?? throw new ArgumentNullException(nameof(soundService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public event EventHandler<Clip>? ClipAdded;

    /// <inheritdoc />
    public async Task<Clip?> GetByIdAsync(string databaseKey, Guid id, CancellationToken cancellationToken = default)
    {
        var repository = GetRepository(databaseKey);
        return await repository.GetByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Clip>> GetRecentAsync(string databaseKey, int count, CancellationToken cancellationToken = default)
    {
        var repository = GetRepository(databaseKey);
        return await repository.GetRecentAsync(count, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Clip>> GetByCollectionAsync(string databaseKey, Guid collectionId, CancellationToken cancellationToken = default)
    {
        var repository = GetRepository(databaseKey);
        return await repository.GetByCollectionAsync(collectionId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Clip>> GetByFolderAsync(string databaseKey, Guid folderId, CancellationToken cancellationToken = default)
    {
        var repository = GetRepository(databaseKey);
        return await repository.GetByFolderAsync(folderId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Clip>> GetFavoritesAsync(string databaseKey, CancellationToken cancellationToken = default)
    {
        var repository = GetRepository(databaseKey);
        return await repository.GetFavoritesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Clip> CreateAsync(string databaseKey, Clip clip, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clip);

        var repository = GetRepository(databaseKey);

        // Check for duplicate by content hash
        var existing = await repository.GetByContentHashAsync(clip.ContentHash, cancellationToken);
        if (existing != null)
        {
            _logger.LogInformation("Clip with content hash {Hash} already exists in database {DatabaseKey}, returning existing clip", clip.ContentHash, databaseKey);
            return existing;
        }

        var createdClip = await repository.CreateAsync(clip, cancellationToken);

        // Raise the ClipAdded event
        ClipAdded?.Invoke(this, createdClip);

        return createdClip;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(string databaseKey, Clip clip, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clip);

        var repository = GetRepository(databaseKey);
        await repository.UpdateAsync(clip, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string databaseKey, Guid id, CancellationToken cancellationToken = default)
    {
        var repository = GetRepository(databaseKey);
        await repository.DeleteAsync(id, cancellationToken);

        // Play erase sound after deletion
        await _soundService.PlaySoundAsync(SoundEvent.Erase, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> DeleteOlderThanAsync(string databaseKey, DateTime olderThan, CancellationToken cancellationToken = default)
    {
        var repository = GetRepository(databaseKey);

        // Get all clips older than the specified date
        var oldClips = await repository.SearchAsync(string.Empty, cancellationToken);
        var clipsToDelete = oldClips.Where(p => p.CapturedAt < olderThan).ToList();

        // Delete each clip
        foreach (var clip in clipsToDelete)
            await repository.DeleteAsync(clip.Id, cancellationToken);

        return clipsToDelete.Count;
    }

    /// <inheritdoc />
    public async Task<bool> IsDuplicateAsync(string databaseKey, string contentHash, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentHash))
            throw new ArgumentException("Content hash cannot be null or empty.", nameof(contentHash));

        var repository = GetRepository(databaseKey);
        var existing = await repository.GetByContentHashAsync(contentHash, cancellationToken);

        return existing != null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ClipData>> GetClipFormatsAsync(string databaseKey, Guid clipId, CancellationToken cancellationToken = default)
    {
        var repository = GetRepository(databaseKey);
        return await repository.GetClipFormatsAsync(clipId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> RenameClipAsync(string databaseKey, Guid clipId, string newTitle, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newTitle))
            throw new ArgumentException("Title cannot be empty", nameof(newTitle));

        var repository = GetRepository(databaseKey);
        var clip = await repository.GetByIdAsync(clipId, cancellationToken);
        if (clip == null)
            return false;

        clip.Title = newTitle;
        return await repository.UpdateAsync(clip, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Clip> CopyClipAsync(string databaseKey, Guid sourceClipId, Guid targetCollectionId, CancellationToken cancellationToken = default)
    {
        var repository = GetRepository(databaseKey);
        var sourceClip = await repository.GetByIdAsync(sourceClipId, cancellationToken);
        if (sourceClip == null)
            throw new ArgumentException($"Source clip {sourceClipId} not found in database {databaseKey}", nameof(sourceClipId));

        // Create new clip with same content
        var copiedClip = new Clip
        {
            Title = $"{sourceClip.Title} (Copy)",
            TextContent = sourceClip.TextContent,
            RtfContent = sourceClip.RtfContent,
            HtmlContent = sourceClip.HtmlContent,
            ImageData = sourceClip.ImageData,
            FilePathsJson = sourceClip.FilePathsJson,
            ContentHash = sourceClip.ContentHash,
            Type = sourceClip.Type,
            CollectionId = targetCollectionId,
            CapturedAt = DateTime.UtcNow,
        };

        var createdClip = await repository.CreateAsync(copiedClip, cancellationToken);

        // Copy all ClipData formats and associated BLOB data
        await CopyClipDataAndBlobsAsync(sourceClipId, createdClip.Id, cancellationToken);

        _logger.LogInformation("Successfully copied clip {SourceClipId} to {TargetClipId} in same database",
            sourceClipId, createdClip.Id);

        return createdClip;
    }

    /// <inheritdoc />
    public async Task<bool> MoveClipAsync(string databaseKey, Guid clipId, Guid targetCollectionId, CancellationToken cancellationToken = default)
    {
        var repository = GetRepository(databaseKey);
        var clip = await repository.GetByIdAsync(clipId, cancellationToken);
        if (clip == null)
            return false;

        // If already in target collection, no update needed
        if (clip.CollectionId == targetCollectionId)
            return true;

        clip.CollectionId = targetCollectionId;
        return await repository.UpdateAsync(clip, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Clip> CopyClipCrossDatabaseAsync(string sourceDatabaseKey,
        Guid sourceClipId,
        string targetDatabaseKey,
        Guid targetCollectionId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Copying clip {ClipId} from database {SourceDb} to database {TargetDb}, collection {CollectionId}",
            sourceClipId, sourceDatabaseKey, targetDatabaseKey, targetCollectionId);

        // Get source clip with all its data
        var sourceRepository = GetRepository(sourceDatabaseKey);
        var sourceClip = await sourceRepository.GetByIdAsync(sourceClipId, cancellationToken);
        if (sourceClip == null)
            throw new ArgumentException($"Source clip {sourceClipId} not found in database {sourceDatabaseKey}", nameof(sourceClipId));

        // Create new clip in target database
        var targetRepository = GetRepository(targetDatabaseKey);
        var copiedClip = new Clip
        {
            Title = $"{sourceClip.Title} (Copy)",
            TextContent = sourceClip.TextContent,
            RtfContent = sourceClip.RtfContent,
            HtmlContent = sourceClip.HtmlContent,
            ImageData = sourceClip.ImageData,
            FilePathsJson = sourceClip.FilePathsJson,
            ContentHash = sourceClip.ContentHash,
            Type = sourceClip.Type,
            CollectionId = targetCollectionId,
            CapturedAt = DateTime.UtcNow,
        };

        var createdClip = await targetRepository.CreateAsync(copiedClip, cancellationToken);

        // Copy all ClipData formats and associated BLOB data
        // Note: Cross-database copy uses the same ClipData/BLOB repositories since they're shared
        await CopyClipDataAndBlobsAsync(sourceClipId, createdClip.Id, cancellationToken);

        _logger.LogInformation("Successfully copied clip {SourceClipId} to new clip {TargetClipId} in target database",
            sourceClipId, createdClip.Id);

        return createdClip;
    }

    /// <inheritdoc />
    public async Task<Clip> MoveClipCrossDatabaseAsync(string sourceDatabaseKey,
        Guid sourceClipId,
        string targetDatabaseKey,
        Guid targetCollectionId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Moving clip {ClipId} from database {SourceDb} to database {TargetDb}, collection {CollectionId}",
            sourceClipId, sourceDatabaseKey, targetDatabaseKey, targetCollectionId);

        // Copy to target database
        var copiedClip = await CopyClipCrossDatabaseAsync(
            sourceDatabaseKey,
            sourceClipId,
            targetDatabaseKey,
            targetCollectionId,
            cancellationToken);

        // Delete from source database
        var sourceRepository = GetRepository(sourceDatabaseKey);
        await sourceRepository.DeleteAsync(sourceClipId, cancellationToken);

        _logger.LogInformation("Successfully moved clip {SourceClipId} to new clip {TargetClipId} in target database",
            sourceClipId, copiedClip.Id);

        return copiedClip;
    }

    /// <summary>
    /// Gets a repository instance for the specified database.
    /// </summary>
    private IClipRepository GetRepository(string databaseKey) => _repositoryFactory.CreateRepository(databaseKey);

    /// <summary>
    /// Copies all ClipData formats and associated BLOB data from source clip to target clip.
    /// </summary>
    /// <param name="sourceClipId">Source clip ID.</param>
    /// <param name="targetClipId">Target clip ID (newly created clip).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task CopyClipDataAndBlobsAsync(Guid sourceClipId, Guid targetClipId, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Copying ClipData and BLOBs from {SourceClipId} to {TargetClipId}", sourceClipId, targetClipId);

        // Create a scope to resolve scoped repositories
        using var scope = _serviceProvider.CreateScope();
        var clipDataRepository = scope.ServiceProvider.GetRequiredService<IClipDataRepository>();
        var blobRepository = scope.ServiceProvider.GetRequiredService<IBlobRepository>();

        // Get all ClipData formats for source clip
        var sourceClipFormats = await clipDataRepository.GetByClipIdAsync(sourceClipId, cancellationToken);

        if (sourceClipFormats.Count == 0)
        {
            _logger.LogDebug("No ClipData formats to copy");
            return;
        }

        // Track which BLOB types we've already copied (avoid duplicates within a clip)
        var copiedBlobTypes = new HashSet<int>();

        // Copy each ClipData format and its associated BLOB
        foreach (var item in sourceClipFormats)
        {
            // Create new ClipData for target clip
            var target = new ClipData
            {
                Id = Guid.NewGuid(),
                ClipId = targetClipId,
                FormatName = item.FormatName,
                Format = item.Format,
                Size = item.Size,
                StorageType = item.StorageType,
            };

            await clipDataRepository.CreateAsync(target, cancellationToken);

            // Copy BLOB data based on StorageType (only once per type)
            if (copiedBlobTypes.Add(item.StorageType))
            {
                switch (item.StorageType)
                {
                    case 1: // BLOBTXT (text formats)
                        await CopyTextBlobsAsync(blobRepository, sourceClipId, targetClipId, cancellationToken);
                        break;

                    case 2: // BLOBJPG (JPEG images)
                        await CopyJpgBlobsAsync(blobRepository, sourceClipId, targetClipId, cancellationToken);
                        break;

                    case 3: // BLOBPNG (PNG images)
                        await CopyPngBlobsAsync(blobRepository, sourceClipId, targetClipId, cancellationToken);
                        break;

                    case 4: // BLOBBLOB (other binary data)
                        await CopyBinaryBlobsAsync(blobRepository, sourceClipId, targetClipId, cancellationToken);
                        break;

                    default:
                        _logger.LogWarning("Unknown StorageType {StorageType} for format {FormatName}",
                            item.StorageType, item.FormatName);

                        break;
                }
            }
        }

        _logger.LogInformation("Successfully copied {Count} ClipData formats and BLOBs", sourceClipFormats.Count);
    }

    /// <summary>
    /// Copies text BLOBs from source to target clip.
    /// </summary>
    private async Task CopyTextBlobsAsync(IBlobRepository blobRepository, Guid sourceClipId, Guid targetClipId, CancellationToken cancellationToken)
    {
        var sourceBlobs = await blobRepository.GetTextByClipIdAsync(sourceClipId, cancellationToken);
        foreach (var item in sourceBlobs)
        {
            var target = new BlobTxt
            {
                Id = Guid.NewGuid(),
                ClipId = targetClipId,
                Data = item.Data,
            };

            await blobRepository.CreateTextAsync(target, cancellationToken);
        }

        _logger.LogDebug("Copied {Count} text BLOBs", sourceBlobs.Count);
    }

    /// <summary>
    /// Copies JPEG BLOBs from source to target clip.
    /// </summary>
    private async Task CopyJpgBlobsAsync(IBlobRepository blobRepository, Guid sourceClipId, Guid targetClipId, CancellationToken cancellationToken)
    {
        var sourceBlobs = await blobRepository.GetJpgByClipIdAsync(sourceClipId, cancellationToken);
        foreach (var item in sourceBlobs)
        {
            var target = new BlobJpg
            {
                Id = Guid.NewGuid(),
                ClipId = targetClipId,
                Data = item.Data,
            };

            await blobRepository.CreateJpgAsync(target, cancellationToken);
        }

        _logger.LogDebug("Copied {Count} JPEG BLOBs", sourceBlobs.Count);
    }

    /// <summary>
    /// Copies PNG BLOBs from source to target clip.
    /// </summary>
    private async Task CopyPngBlobsAsync(IBlobRepository blobRepository, Guid sourceClipId, Guid targetClipId, CancellationToken cancellationToken)
    {
        var sourceBlobs = await blobRepository.GetPngByClipIdAsync(sourceClipId, cancellationToken);
        foreach (var item in sourceBlobs)
        {
            var target = new BlobPng
            {
                Id = Guid.NewGuid(),
                ClipId = targetClipId,
                Data = item.Data,
            };

            await blobRepository.CreatePngAsync(target, cancellationToken);
        }

        _logger.LogDebug("Copied {Count} PNG BLOBs", sourceBlobs.Count);
    }

    /// <summary>
    /// Copies binary BLOBs from source to target clip.
    /// </summary>
    private async Task CopyBinaryBlobsAsync(IBlobRepository blobRepository, Guid sourceClipId, Guid targetClipId, CancellationToken cancellationToken)
    {
        var sourceBlobs = await blobRepository.GetBlobByClipIdAsync(sourceClipId, cancellationToken);
        foreach (var item in sourceBlobs)
        {
            var target = new BlobBlob
            {
                Id = Guid.NewGuid(),
                ClipId = targetClipId,
                Data = item.Data,
            };

            await blobRepository.CreateBlobAsync(target, cancellationToken);
        }

        _logger.LogDebug("Copied {Count} binary BLOBs", sourceBlobs.Count);
    }
}
