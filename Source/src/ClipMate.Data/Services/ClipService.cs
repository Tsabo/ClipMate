using System.Security.Cryptography;
using System.Text;
using ClipMate.Core.Events;
using ClipMate.Core.Helpers;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Core.ValueObjects;
using ClipMate.Data.Repositories;
using ClipMate.Platform.Helpers;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Service for managing clips (CRUD operations, history management).
/// Registered as singleton to support multi-database operations via repository factories.
/// </summary>
public class ClipService : IClipService
{
    private readonly IDecryptedBlobCacheService _blobCacheService;
    private readonly IClipStore _clipStore;
    private readonly IDatabases _databases;
    private readonly IConfigurationService _configurationService;
    private readonly IDatabaseContextFactory _databaseContextFactory;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<ClipService> _logger;
    private readonly IMessenger _messenger;

    public ClipService(IDatabaseContextFactory databaseContextFactory,
        IClipStore clipStore,
        IConfigurationService configurationService,
        IEncryptionService encryptionService,
        IDecryptedBlobCacheService blobCacheService,
        IDatabases databases,
        IMessenger messenger,
        ILogger<ClipService> logger)
    {
        _databaseContextFactory = databaseContextFactory ?? throw new ArgumentNullException(nameof(databaseContextFactory));
        _clipStore = clipStore ?? throw new ArgumentNullException(nameof(clipStore));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        _blobCacheService = blobCacheService ?? throw new ArgumentNullException(nameof(blobCacheService));
        _databases = databases ?? throw new ArgumentNullException(nameof(databases));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
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
    public async Task<IReadOnlyList<Clip>> GetByCollectionAsync(string databaseKey, Guid collectionId, CancellationToken cancellationToken = default)
    {
        var repository = GetRepository(databaseKey);
        return await repository.GetByCollectionAsync(collectionId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Clip>> GetFavoritesAsync(string databaseKey, CancellationToken cancellationToken = default)
    {
        var repository = GetRepository(databaseKey);
        return await repository.GetFavoritesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetDistinctFormatsAsync(string databaseKey, CancellationToken cancellationToken = default)
    {
        var repository = _databaseContextFactory.GetClipDataRepository(databaseKey);
        return await repository.GetDistinctFormatsAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Clip> CreateAsync(string databaseKey, Clip clip, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clip);

        // A clip made by hand or imported is always added; only capture checks for Duplicates
        var createdClip = ((ClipAdded)await _clipStore.AddAsync(databaseKey, clip, clip.CollectionId, clip.FolderId, false, cancellationToken)).Clip;

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
    }

    /// <inheritdoc />
    public async Task<int> DeleteOlderThanAsync(string databaseKey, DateTime olderThan, CancellationToken cancellationToken = default)
    {
        var repository = GetRepository(databaseKey);

        // Get all clips older than the specified date
        var oldClips = await repository.SearchAsync(string.Empty, cancellationToken);
        var clipsToDelete = oldClips.Where(p => p.CapturedAt < olderThan).ToList();

        // Delete each clip
        foreach (var item in clipsToDelete)
            await repository.DeleteAsync(item.Id, cancellationToken);

        return clipsToDelete.Count;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ClipData>> GetClipFormatsAsync(string databaseKey, Guid clipId, CancellationToken cancellationToken = default)
    {
        var repository = GetRepository(databaseKey);
        return await repository.GetClipFormatsAsync(clipId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ConvertFilePointerToTextAsync(string databaseKey, Guid clipId, CancellationToken cancellationToken = default)
    {
        var repository = GetRepository(databaseKey);
        return await repository.ConvertFilePointerToTextAsync(clipId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Clip?> TransformTextAsync(string databaseKey, Guid clipId, Func<string, string> transform, Func<string, string>? titleTransform, CancellationToken cancellationToken = default)
    {
        var repository = GetRepository(databaseKey);
        return await repository.TransformTextAsync(clipId, transform, titleTransform, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> MoveClipSortOrderAsync(string databaseKey, Guid clipId, bool toTop, CancellationToken cancellationToken = default)
    {
        var repository = GetRepository(databaseKey);
        return await repository.MoveClipSortOrderAsync(clipId, toTop, cancellationToken);
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

        var createdClip = ((ClipAdded)await _clipStore.AddAsync(databaseKey, copiedClip, targetCollectionId, null, false, cancellationToken)).Clip;

        // Copy all ClipData formats and associated BLOB data (same database for both source and target)
        await CopyClipDataAndBlobsAsync(databaseKey, sourceClipId, databaseKey, createdClip.Id, cancellationToken);

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
    public async Task MoveClipsToCollectionAsync(string databaseKey, List<Guid> clipIds, Guid targetCollectionId, CancellationToken cancellationToken = default)
    {
        var repository = GetRepository(databaseKey);
        await repository.MoveClipsToCollectionAsync(clipIds, targetCollectionId);
        _logger.LogInformation("Moved {Count} clips to collection {CollectionId} in database {DatabaseKey}",
            clipIds.Count, targetCollectionId, databaseKey);
    }

    /// <inheritdoc />
    public async Task SoftDeleteClipsAsync(string databaseKey, List<Guid> clipIds, CancellationToken cancellationToken = default)
    {
        var repository = GetRepository(databaseKey);
        await repository.SoftDeleteClipsAsync(clipIds);
        _logger.LogInformation("Soft-deleted {Count} clips to Trashcan in database {DatabaseKey}",
            clipIds.Count, databaseKey);
    }

    /// <inheritdoc />
    public async Task RestoreClipsAsync(string databaseKey, List<Guid> clipIds, Guid targetCollectionId, CancellationToken cancellationToken = default)
    {
        var repository = GetRepository(databaseKey);
        await repository.RestoreClipsAsync(clipIds, targetCollectionId);
        _logger.LogInformation("Restored {Count} clips from Trashcan to collection {CollectionId} in database {DatabaseKey}",
            clipIds.Count, targetCollectionId, databaseKey);
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
        var copiedClip = new Clip
        {
            Title = $"{sourceClip.Title} (Copy)",
            // NOTE: Don't copy non-persisted properties (TextContent, RtfContent, HtmlContent, ImageData, FilePathsJson)
            // These are loaded from BLOB tables when needed and aren't stored in the Clips table
            ContentHash = sourceClip.ContentHash,
            Type = sourceClip.Type,
            SortKey = sourceClip.SortKey,
            CollectionId = targetCollectionId,
            CapturedAt = sourceClip.CapturedAt, // Preserve original capture timestamp
        };

        var createdClip = ((ClipAdded)await _clipStore.AddAsync(targetDatabaseKey, copiedClip, targetCollectionId, null, false, cancellationToken)).Clip;

        // Copy all ClipData formats and associated BLOB data from source database to target database
        await CopyClipDataAndBlobsAsync(sourceDatabaseKey, sourceClipId, targetDatabaseKey, createdClip.Id, cancellationToken);

        _logger.LogInformation("Successfully copied clip {SourceClipId} to new clip {TargetClipId} in target database",
            sourceClipId, createdClip.Id);

        return createdClip;
    }

    /// <inheritdoc />
    public async Task<Clip> CopyToLocalAsync(string sourceDatabaseKey,
        Guid sourceClipId,
        string targetDatabaseKey,
        Guid targetCollectionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDatabaseKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDatabaseKey);

        if (_databases.Get(targetDatabaseKey).IsReadOnly)
            throw new InvalidOperationException($"Copy to local cannot copy into a read-only Database: {DatabaseMessages.ReadOnly}");

        // Read everything first (a Remote Database is read from its Peer), so a failed read writes nothing locally
        var content = await _databases.Get(sourceDatabaseKey).GetClipContentAsync(sourceClipId, cancellationToken)
                      ?? throw new InvalidOperationException($"Clip {sourceClipId} no longer exists in its Database.");

        var copy = CreateLocalCopy(content.Clip, targetCollectionId);

        var clipRepository = GetRepository(targetDatabaseKey);
        var clipDataRepository = _databaseContextFactory.GetClipDataRepository(targetDatabaseKey);
        var blobRepository = _databaseContextFactory.GetBlobRepository(targetDatabaseKey);

        await _clipStore.AddAsync(targetDatabaseKey, copy, copy.CollectionId, copy.FolderId, false, cancellationToken);

        try
        {
            // Every format and blob gets a new ID, so the same clip can be copied more than once
            var formatIds = new Dictionary<Guid, Guid>();
            foreach (var item in content.Formats)
            {
                var format = new ClipData
                {
                    Id = Guid.NewGuid(),
                    ClipId = copy.Id,
                    FormatName = item.FormatName,
                    Format = item.Format,
                    Size = item.Size,
                    StorageType = item.StorageType,
                };

                await clipDataRepository.CreateAsync(format, cancellationToken);
                formatIds[item.Id] = format.Id;
            }

            // Blob content is copied exactly as stored: an encrypted clip stays encrypted
            foreach (var item in content.TextBlobs.Where(p => formatIds.ContainsKey(p.ClipDataId)))
            {
                await blobRepository.CreateTextAsync(new BlobTxt
                {
                    Id = Guid.NewGuid(),
                    ClipDataId = formatIds[item.ClipDataId],
                    ClipId = copy.Id,
                    Data = item.Data,
                }, cancellationToken);
            }

            foreach (var item in content.JpgBlobs.Where(p => formatIds.ContainsKey(p.ClipDataId)))
            {
                await blobRepository.CreateJpgAsync(new BlobJpg
                {
                    Id = Guid.NewGuid(),
                    ClipDataId = formatIds[item.ClipDataId],
                    ClipId = copy.Id,
                    Data = item.Data,
                }, cancellationToken);
            }

            foreach (var item in content.PngBlobs.Where(p => formatIds.ContainsKey(p.ClipDataId)))
            {
                await blobRepository.CreatePngAsync(new BlobPng
                {
                    Id = Guid.NewGuid(),
                    ClipDataId = formatIds[item.ClipDataId],
                    ClipId = copy.Id,
                    Data = item.Data,
                }, cancellationToken);
            }

            foreach (var item in content.BinaryBlobs.Where(p => formatIds.ContainsKey(p.ClipDataId)))
            {
                await blobRepository.CreateBlobAsync(new BlobBlob
                {
                    Id = Guid.NewGuid(),
                    ClipDataId = formatIds[item.ClipDataId],
                    ClipId = copy.Id,
                    Data = item.Data,
                }, cancellationToken);
            }
        }
        catch
        {
            // Never leave a half-copied clip behind
            await DeletePartialCopyAsync(targetDatabaseKey, copy.Id);
            throw;
        }

        _logger.LogInformation("Copied clip {SourceClipId} from {SourceDatabaseKey} to local clip {ClipId} in {TargetDatabaseKey}",
            sourceClipId, sourceDatabaseKey, copy.Id, targetDatabaseKey);

        return await clipRepository.GetByIdAsync(copy.Id, cancellationToken) ?? copy;
    }

    /// <summary>
    /// Builds the new local clip for Copy to local: every stored field of the original, including the encryption
    /// fields (salt, IV, method) that keep an encrypted clip decryptable, but with a new ID, the target collection
    /// and no folder. The title is copied as stored, since an encrypted title is ciphertext. The user is left unset, as for
    /// a captured clip: a user ID only means something in the database it came from.
    /// </summary>
    private static Clip CreateLocalCopy(Clip source, Guid targetCollectionId) => new()
    {
        Id = Guid.NewGuid(),
        CollectionId = targetCollectionId,
        FolderId = null,
        UserId = null,
        Title = source.Title,
        Creator = source.Creator,
        CapturedAt = source.CapturedAt,
        SourceUrl = source.SourceUrl,
        CustomTitle = source.CustomTitle,
        Locale = source.Locale,
        WrapCheck = source.WrapCheck,
        Encrypted = source.Encrypted,
        EncryptionSalt = source.EncryptionSalt,
        EncryptionIv = source.EncryptionIv,
        EncryptionMethod = source.EncryptionMethod,
        Icons = source.Icons,
        Size = source.Size,
        Checksum = source.Checksum,
        ViewTab = source.ViewTab,
        Macro = source.Macro,
        LastModified = source.LastModified,
        ContentHash = source.ContentHash,
        SourceApplicationName = source.SourceApplicationName,
        SourceApplicationTitle = source.SourceApplicationTitle,
        PasteCount = source.PasteCount,
        IsFavorite = source.IsFavorite,
        Label = source.Label,
        Type = source.Type,
    };

    private async Task DeletePartialCopyAsync(string databaseKey, Guid clipId)
    {
        try
        {
            await _databaseContextFactory.GetBlobRepository(databaseKey).DeleteByClipIdAsync(clipId);
            await _databaseContextFactory.GetClipDataRepository(databaseKey).DeleteByClipIdAsync(clipId);
            await GetRepository(databaseKey).DeleteAsync(clipId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not remove the partial copy {ClipId} from {DatabaseKey}", clipId, databaseKey);
        }
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

    /// <inheritdoc />
    public async Task LoadBlobDataAsync(string databaseKey, Clip clip, CancellationToken cancellationToken = default)
    {
        try
        {
            // Read the content from the clip's Database (a Remote Database is read from its Peer)
            var content = await _databases.Get(databaseKey).GetClipContentAsync(clip.Id, cancellationToken);
            if (content == null || content.Formats.Count == 0)
            {
                _logger.LogDebug("No ClipData found for clip: {ClipId}", clip.Id);
                return;
            }

            content.PopulateContent(clip);

            _logger.LogDebug("Loaded blob data for clip: {ClipId} from database {DatabaseKey}", clip.Id, databaseKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load blob data for clip {ClipId} from database {DatabaseKey}", clip.Id, databaseKey);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Clip>> ExecuteSqlQueryAsync(string databaseKey, string sqlQuery, int retentionLimit, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sqlQuery))
            throw new ArgumentException("SQL query cannot be null or empty.", nameof(sqlQuery));

        // Replace placeholders with actual values
        var processedQuery = VirtualCollectionSql.ReplacePlaceholders(sqlQuery, retentionLimit);

        _logger.LogDebug("Executing virtual collection SQL query: {Query}", processedQuery);

        try
        {
            // Use repository's ExecuteSqlQueryAsync which handles Dapper mapping and LoadFormatFlagsAsync
            var repository = GetRepository(databaseKey);
            var clips = await repository.ExecuteSqlQueryAsync(processedQuery, cancellationToken);

            _logger.LogInformation("Virtual collection SQL query returned {Count} clips", clips.Count);

            return clips;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute virtual collection SQL query: {Query}", processedQuery);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ClipPage> ExecuteSqlQueryPageAsync(string databaseKey, string sqlQuery, int retentionLimit, int offset, int count,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sqlQuery))
            throw new ArgumentException("SQL query cannot be null or empty.", nameof(sqlQuery));

        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        var processedQuery = VirtualCollectionSql.ReplacePlaceholders(sqlQuery, retentionLimit);
        _logger.LogDebug("Executing virtual collection SQL query for clips {Offset} to {Last}: {Query}", offset, offset + count - 1, processedQuery);

        try
        {
            return await GetRepository(databaseKey).ExecuteSqlPageAsync(processedQuery, offset, count, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to execute virtual collection SQL query: {Query}", processedQuery);
            throw;
        }
    }

    /// <summary>
    /// Gets a repository instance for the specified database.
    /// </summary>
    private IClipRepository GetRepository(string databaseKey) => _databaseContextFactory.GetClipRepository(databaseKey);

    /// <summary>
    /// Copies all ClipData formats and associated BLOB data from source clip to target clip.
    /// Supports cross-database operations by using database-specific repositories.
    /// </summary>
    /// <param name="sourceDatabaseKey">Source database key.</param>
    /// <param name="sourceClipId">Source clip ID.</param>
    /// <param name="targetDatabaseKey">Target database key.</param>
    /// <param name="targetClipId">Target clip ID (newly created clip).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task CopyClipDataAndBlobsAsync(string sourceDatabaseKey, Guid sourceClipId, string targetDatabaseKey, Guid targetClipId, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Copying ClipData and BLOBs from {SourceDb}:{SourceClipId} to {TargetDb}:{TargetClipId}",
            sourceDatabaseKey, sourceClipId, targetDatabaseKey, targetClipId);

        // Get database-specific repositories
        var sourceClipDataRepo = _databaseContextFactory.GetClipDataRepository(sourceDatabaseKey);
        var sourceBlobRepo = _databaseContextFactory.GetBlobRepository(sourceDatabaseKey);
        var targetClipDataRepo = _databaseContextFactory.GetClipDataRepository(targetDatabaseKey);
        var targetBlobRepo = _databaseContextFactory.GetBlobRepository(targetDatabaseKey);

        // Get all ClipData formats for source clip
        var sourceClipFormats = await sourceClipDataRepo.GetByClipIdAsync(sourceClipId, cancellationToken);

        _logger.LogInformation("Found {Count} ClipData formats in source clip {SourceClipId}", sourceClipFormats.Count, sourceClipId);

        if (sourceClipFormats.Count == 0)
        {
            _logger.LogWarning("No ClipData formats to copy for clip {SourceClipId}", sourceClipId);
            return;
        }

        // Create a mapping between source and target ClipDataIds
        var clipDataIdMap = new Dictionary<Guid, Guid>();

        // Copy each ClipData format
        foreach (var item in sourceClipFormats)
        {
            // Create new ClipData for target clip
            var targetClipDataId = Guid.NewGuid();
            var target = new ClipData
            {
                Id = targetClipDataId,
                ClipId = targetClipId,
                FormatName = item.FormatName,
                Format = item.Format,
                Size = item.Size,
                StorageType = item.StorageType,
            };

            await targetClipDataRepo.CreateAsync(target, cancellationToken);
            _logger.LogDebug("Created ClipData {ClipDataId} for target clip {TargetClipId}, Format: {Format}", targetClipDataId, targetClipId, item.Format);

            // Map source ClipDataId to target ClipDataId
            clipDataIdMap[item.Id] = targetClipDataId;
        }

        // Verify ClipData was created
        var verifyCount = await targetClipDataRepo.GetByClipIdAsync(targetClipId, cancellationToken);
        _logger.LogInformation("Verification: Found {Count} ClipData entries for target clip {TargetClipId}", verifyCount.Count, targetClipId);

        // Now copy BLOBs with the correct ClipDataId mappings
        // Group by StorageType to copy each type once
        var storageTypes = sourceClipFormats.Select(p => p.StorageType).Distinct();

        foreach (var item in storageTypes)
        {
            switch (item)
            {
                case 1: // BLOBTXT (text formats)
                    await CopyTextBlobsAsync(sourceBlobRepo, targetBlobRepo, sourceClipId, targetClipId, clipDataIdMap, cancellationToken);
                    break;

                case 2: // BLOBJPG (JPEG images)
                    await CopyJpgBlobsAsync(sourceBlobRepo, targetBlobRepo, sourceClipId, targetClipId, clipDataIdMap, cancellationToken);
                    break;

                case 3: // BLOBPNG (PNG images)
                    await CopyPngBlobsAsync(sourceBlobRepo, targetBlobRepo, sourceClipId, targetClipId, clipDataIdMap, cancellationToken);
                    break;

                case 4: // BLOBBLOB (other binary data)
                    await CopyBinaryBlobsAsync(sourceBlobRepo, targetBlobRepo, sourceClipId, targetClipId, clipDataIdMap, cancellationToken);
                    break;

                default:
                    _logger.LogWarning("Unknown StorageType {StorageType}", item);
                    break;
            }
        }

        _logger.LogInformation("Successfully copied {Count} ClipData formats and BLOBs", sourceClipFormats.Count);
    }

    /// <summary>
    /// Copies text BLOBs from source to target clip.
    /// </summary>
    /// <param name="sourceBlobRepo"></param>
    /// <param name="targetBlobRepo"></param>
    /// <param name="sourceClipId"></param>
    /// <param name="targetClipId"></param>
    /// <param name="clipDataIdMap">Mapping from source ClipDataId to target ClipDataId.</param>
    /// <param name="cancellationToken"></param>
    private async Task CopyTextBlobsAsync(IBlobRepository sourceBlobRepo, IBlobRepository targetBlobRepo, Guid sourceClipId, Guid targetClipId, Dictionary<Guid, Guid> clipDataIdMap, CancellationToken cancellationToken)
    {
        var sourceBlobs = await sourceBlobRepo.GetTextByClipIdAsync(sourceClipId, cancellationToken);
        foreach (var item in sourceBlobs)
        {
            // Map the source ClipDataId to the target ClipDataId
            if (!clipDataIdMap.TryGetValue(item.ClipDataId, out var targetClipDataId))
            {
                _logger.LogWarning("No ClipDataId mapping found for source ClipDataId {ClipDataId}, skipping BLOB", item.ClipDataId);
                continue;
            }

            var target = new BlobTxt
            {
                Id = Guid.NewGuid(),
                ClipDataId = targetClipDataId,
                ClipId = targetClipId,
                Data = item.Data,
            };

            await targetBlobRepo.CreateTextAsync(target, cancellationToken);
        }

        _logger.LogDebug("Copied {Count} text BLOBs", sourceBlobs.Count);
    }

    /// <summary>
    /// Copies JPEG BLOBs from source to target clip.
    /// </summary>
    /// <param name="sourceBlobRepo"></param>
    /// <param name="targetBlobRepo"></param>
    /// <param name="sourceClipId"></param>
    /// <param name="targetClipId"></param>
    /// <param name="clipDataIdMap">Mapping from source ClipDataId to target ClipDataId.</param>
    /// <param name="cancellationToken"></param>
    private async Task CopyJpgBlobsAsync(IBlobRepository sourceBlobRepo, IBlobRepository targetBlobRepo, Guid sourceClipId, Guid targetClipId, Dictionary<Guid, Guid> clipDataIdMap, CancellationToken cancellationToken)
    {
        var sourceBlobs = await sourceBlobRepo.GetJpgByClipIdAsync(sourceClipId, cancellationToken);
        foreach (var item in sourceBlobs)
        {
            // Map the source ClipDataId to the target ClipDataId
            if (!clipDataIdMap.TryGetValue(item.ClipDataId, out var targetClipDataId))
            {
                _logger.LogWarning("No ClipDataId mapping found for source ClipDataId {ClipDataId}, skipping BLOB", item.ClipDataId);
                continue;
            }

            var target = new BlobJpg
            {
                Id = Guid.NewGuid(),
                ClipDataId = targetClipDataId,
                ClipId = targetClipId,
                Data = item.Data,
            };

            await targetBlobRepo.CreateJpgAsync(target, cancellationToken);
        }

        _logger.LogDebug("Copied {Count} JPEG BLOBs", sourceBlobs.Count);
    }

    /// <summary>
    /// Copies PNG BLOBs from source to target clip.
    /// </summary>
    /// <param name="sourceBlobRepo"></param>
    /// <param name="targetBlobRepo"></param>
    /// <param name="sourceClipId"></param>
    /// <param name="targetClipId"></param>
    /// <param name="clipDataIdMap">Mapping from source ClipDataId to target ClipDataId.</param>
    /// <param name="cancellationToken"></param>
    private async Task CopyPngBlobsAsync(IBlobRepository sourceBlobRepo, IBlobRepository targetBlobRepo, Guid sourceClipId, Guid targetClipId, Dictionary<Guid, Guid> clipDataIdMap, CancellationToken cancellationToken)
    {
        var sourceBlobs = await sourceBlobRepo.GetPngByClipIdAsync(sourceClipId, cancellationToken);
        foreach (var item in sourceBlobs)
        {
            // Map the source ClipDataId to the target ClipDataId
            if (!clipDataIdMap.TryGetValue(item.ClipDataId, out var targetClipDataId))
            {
                _logger.LogWarning("No ClipDataId mapping found for source ClipDataId {ClipDataId}, skipping BLOB", item.ClipDataId);
                continue;
            }

            var target = new BlobPng
            {
                Id = Guid.NewGuid(),
                ClipDataId = targetClipDataId,
                ClipId = targetClipId,
                Data = item.Data,
            };

            await targetBlobRepo.CreatePngAsync(target, cancellationToken);
        }

        _logger.LogDebug("Copied {Count} PNG BLOBs", sourceBlobs.Count);
    }

    /// <summary>
    /// Copies binary BLOBs from source to target clip.
    /// </summary>
    /// <param name="sourceBlobRepo"></param>
    /// <param name="targetBlobRepo"></param>
    /// <param name="sourceClipId"></param>
    /// <param name="targetClipId"></param>
    /// <param name="clipDataIdMap">Mapping from source ClipDataId to target ClipDataId.</param>
    /// <param name="cancellationToken"></param>
    private async Task CopyBinaryBlobsAsync(IBlobRepository sourceBlobRepo, IBlobRepository targetBlobRepo, Guid sourceClipId, Guid targetClipId, Dictionary<Guid, Guid> clipDataIdMap, CancellationToken cancellationToken)
    {
        var sourceBlobs = await sourceBlobRepo.GetBlobByClipIdAsync(sourceClipId, cancellationToken);
        foreach (var item in sourceBlobs)
        {
            // Map the source ClipDataId to the target ClipDataId
            if (!clipDataIdMap.TryGetValue(item.ClipDataId, out var targetClipDataId))
            {
                _logger.LogWarning("No ClipDataId mapping found for source ClipDataId {ClipDataId}, skipping BLOB", item.ClipDataId);
                continue;
            }

            var target = new BlobBlob
            {
                Id = Guid.NewGuid(),
                ClipDataId = targetClipDataId,
                ClipId = targetClipId,
                Data = item.Data,
            };

            await targetBlobRepo.CreateBlobAsync(target, cancellationToken);
        }

        _logger.LogDebug("Copied {Count} binary BLOBs", sourceBlobs.Count);
    }

    /// <summary>
    /// Resolves a database key to its file path using configuration.
    /// If the input is already a file path (contains path separators), returns it as-is.
    /// </summary>
#pragma warning disable IDE0051 // Remove unused private members - may be used in future
    private string ResolveDatabaseKeyToPath(string databaseKeyOrPath)
#pragma warning restore IDE0051
    {
        // If it looks like a file path (contains directory separators), return as-is
        if (databaseKeyOrPath.Contains(Path.DirectorySeparatorChar) ||
            databaseKeyOrPath.Contains(Path.AltDirectorySeparatorChar))
            return databaseKeyOrPath;

        // Otherwise, try to resolve it as a database key from configuration
        if (_configurationService.Configuration.Databases.TryGetValue(databaseKeyOrPath, out var dbConfig))
            return dbConfig.FilePath;

        // If not found in configuration, assume it's a path anyway
        _logger.LogWarning("Database key '{DatabaseKey}' not found in configuration, treating as file path", databaseKeyOrPath);
        return databaseKeyOrPath;
    }

    #region Encryption Methods

    /// <inheritdoc />
    public async Task<int> EncryptClipsAsync(string databaseKey, IReadOnlyList<Guid> clipIds, EncryptionKey encryptionKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clipIds);
        ArgumentNullException.ThrowIfNull(encryptionKey);

        var encryptedCount = 0;
        var clipRepository = GetRepository(databaseKey);
        var clipDataRepository = _databaseContextFactory.GetClipDataRepository(databaseKey);
        var blobRepository = _databaseContextFactory.GetBlobRepository(databaseKey);

        foreach (var item in clipIds)
        {
            try
            {
                var clip = await clipRepository.GetByIdAsync(item, cancellationToken);
                if (clip == null)
                {
                    _logger.LogWarning("Clip {ClipId} not found, skipping encryption", item);
                    continue;
                }

                // Skip if already encrypted
                if (clip.Encrypted)
                {
                    _logger.LogInformation("Clip {ClipId} is already encrypted, skipping", item);
                    continue;
                }

                // Get all ClipData entries
                var clipDataEntries = await clipDataRepository.GetByClipIdAsync(item, cancellationToken);

                if (clipDataEntries.Count == 0)
                {
                    _logger.LogWarning("Clip {ClipId} has no ClipData entries, skipping encryption", item);
                    continue;
                }

                // Get all BLOBs for this clip
                var textBlobs = await blobRepository.GetTextByClipIdAsync(item, cancellationToken);
                var jpgBlobs = await blobRepository.GetJpgByClipIdAsync(item, cancellationToken);
                var pngBlobs = await blobRepository.GetPngByClipIdAsync(item, cancellationToken);
                var binaryBlobs = await blobRepository.GetBlobByClipIdAsync(item, cancellationToken);

                // Encrypt Title and all BLOBs with one shared Salt/IV, then persist the BLOBs
                await ClipContentEncryption.EncryptAsync(_encryptionService, encryptionKey, clip, textBlobs, jpgBlobs, pngBlobs, binaryBlobs);

                foreach (var blob in textBlobs)
                    await blobRepository.UpdateTextAsync(blob, cancellationToken);

                foreach (var blob in jpgBlobs)
                    await blobRepository.UpdateJpgAsync(blob, cancellationToken);

                foreach (var blob in pngBlobs)
                    await blobRepository.UpdatePngAsync(blob, cancellationToken);

                foreach (var blob in binaryBlobs)
                    await blobRepository.UpdateBlobAsync(blob, cancellationToken);

                // SECURITY: Clear transient properties to prevent plain text from remaining in memory
                // and being returned by subsequent queries (Entity Framework tracking).
                // These properties are marked as [NotMapped] and only loaded from BLOB tables when needed.
                clip.TextContent = null;
                clip.RtfContent = null;
                clip.HtmlContent = null;
                clip.ImageData = null;

                await clipRepository.UpdateAsync(clip, cancellationToken);
                encryptedCount++;

                _logger.LogInformation("Successfully encrypted clip {ClipId}", item);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error encrypting clip {ClipId}", item);
            }
        }

        return encryptedCount;
    }

    /// <inheritdoc />
    public async Task<int> DecryptClipsAsync(string databaseKey, IReadOnlyList<Guid> clipIds, EncryptionKey encryptionKey, bool isPermanent = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clipIds);
        ArgumentNullException.ThrowIfNull(encryptionKey);

        var decryptedCount = 0;
        var clipRepository = GetRepository(databaseKey);

        foreach (var item in clipIds)
        {
            try
            {
                var clip = await clipRepository.GetByIdAsync(item, cancellationToken);
                if (clip == null)
                {
                    _logger.LogWarning("Clip {ClipId} not found, skipping decryption", item);
                    continue;
                }

                var result = await DecryptClipAsync(databaseKey, clip, encryptionKey, isPermanent, cancellationToken);
                if (result)
                    decryptedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrypting clip {ClipId} - wrong key or corrupted data", item);
            }
        }

        return decryptedCount;
    }

    /// <inheritdoc />
    public async Task<bool> DecryptClipAsync(string databaseKey, Clip clip, EncryptionKey encryptionKey, bool isPermanent = false, CancellationToken cancellationToken = default)
    {
        // Skip if not encrypted
        if (!clip.Encrypted)
        {
            _logger.LogInformation("Clip {ClipId} is not encrypted, skipping", clip.Id);
            return false;
        }

        // Skip if already decrypted
        if (clip.IsDecrypted)
        {
            _logger.LogInformation("Clip {ClipId} is already decrypted, skipping", clip.Id);
            return false;
        }

        // Clips encrypted before AES-GCM store "AES-256" (or no method) and a per-clip IV
        var metadata = new EncryptionMetadata(
            clip.EncryptionSalt ?? string.Empty,
            clip.EncryptionIv ?? string.Empty,
            string.IsNullOrEmpty(clip.EncryptionMethod)
                ? EncryptionMetadata.LegacyCbcAlgorithm
                : clip.EncryptionMethod,
            0);

        // Validate encryption metadata
        if (string.IsNullOrEmpty(metadata.Salt) || (!metadata.IsAuthenticated && string.IsNullOrEmpty(metadata.IV)))
        {
            _logger.LogError("Clip {ClipId} is missing encryption metadata, cannot decrypt", clip.Id);
            return false;
        }

        // A Remote Database is read-only: its clips can be decrypted for viewing here, never decrypted in place
        if (isPermanent && _databases.Get(databaseKey).IsReadOnly)
            throw new InvalidOperationException($"A clip in a read-only Database cannot be decrypted permanently: {DatabaseMessages.ReadOnly}");

        // Permanent decryption writes back to the local Database; decryption for viewing only reads the stored
        // ciphertext, which for a Remote Database comes from its Peer
        var clipRepository = isPermanent ? GetRepository(databaseKey) : null;
        var blobRepository = isPermanent ? _databaseContextFactory.GetBlobRepository(databaseKey) : null;

        // Get all BLOBs for this clip
        IReadOnlyList<BlobTxt> textBlobs;
        IReadOnlyList<BlobJpg> jpgBlobs;
        IReadOnlyList<BlobPng> pngBlobs;
        IReadOnlyList<BlobBlob> binaryBlobs;
        if (blobRepository != null)
        {
            textBlobs = await blobRepository.GetTextByClipIdAsync(clip.Id, cancellationToken);
            jpgBlobs = await blobRepository.GetJpgByClipIdAsync(clip.Id, cancellationToken);
            pngBlobs = await blobRepository.GetPngByClipIdAsync(clip.Id, cancellationToken);
            binaryBlobs = await blobRepository.GetBlobByClipIdAsync(clip.Id, cancellationToken);
        }
        else
        {
            var content = await _databases.Get(databaseKey).GetClipContentAsync(clip.Id, cancellationToken);
            textBlobs = content?.TextBlobs ?? [];
            jpgBlobs = content?.JpgBlobs ?? [];
            pngBlobs = content?.PngBlobs ?? [];
            binaryBlobs = content?.BinaryBlobs ?? [];
        }

        // Decrypt into detached copies, so nothing is written (or left modified on a tracked entity) unless every
        // field decrypts: a wrong key or altered data must never leave a clip half decrypted
        var decryptedText = textBlobs.Select(b => new BlobTxt { Id = b.Id, ClipId = b.ClipId, ClipDataId = b.ClipDataId, Data = b.Data }).ToList();
        var decryptedJpg = jpgBlobs.Select(b => new BlobJpg { Id = b.Id, ClipId = b.ClipId, ClipDataId = b.ClipDataId, Data = b.Data }).ToList();
        var decryptedPng = pngBlobs.Select(b => new BlobPng { Id = b.Id, ClipId = b.ClipId, ClipDataId = b.ClipDataId, Data = b.Data }).ToList();
        var decryptedBinary = binaryBlobs.Select(b => new BlobBlob { Id = b.Id, ClipId = b.ClipId, ClipDataId = b.ClipDataId, Data = b.Data }).ToList();

        // AES-GCM authenticates every field, so any failure is a wrong key or altered data and fails the clip. The
        // legacy CBC format tolerates fields that are already plain text (from an old bug where the Encrypted flag
        // wasn't cleared), but a failure of every field means the key is wrong.
        var decryptedFields = 0;
        var failedFields = 0;

        async Task<byte[]?> TryDecryptFieldAsync(byte[] encryptedBytes, string field, Guid id)
        {
            try
            {
                var decryptedBytes = await _encryptionService.DecryptAsync(encryptedBytes, metadata, encryptionKey, ClipContentEncryption.Iterations);
                decryptedFields++;
                _logger.LogDebug("Decrypted {Field} for {Id} (persistent: {IsPermanent})", field, id, isPermanent);
                return decryptedBytes;
            }
            catch (CryptographicException) when (metadata.IsAuthenticated)
            {
                _logger.LogWarning("Authentication failed for the {Field} of clip {ClipId}: wrong key or altered data", field, clip.Id);
                throw;
            }
            catch (CryptographicException ex)
            {
                failedFields++;
                _logger.LogWarning(ex, "{Field} for {Id} could not be decrypted (legacy format) - may already be plain text, leaving as-is", field, id);
                return null;
            }
        }

        foreach (var item in decryptedText)
        {
            byte[] encryptedBytes;
            try
            {
                encryptedBytes = Convert.FromBase64String(item.Data);
            }
            catch (FormatException ex) when (metadata.IsAuthenticated)
            {
                // AES-GCM always stores TEXT as Base64 ciphertext: anything else was altered
                throw new CryptographicException("Encrypted text is not valid Base64. The data was altered.", ex);
            }
            catch (FormatException ex)
            {
                // Handle corrupted data (e.g., from old encryption that didn't properly encrypt all fields)
                _logger.LogWarning(ex, "TEXT BLOB for ClipData {ClipDataId} contains invalid Base64 - may be corrupted, leaving as-is", item.ClipDataId);
                continue;
            }

            if (await TryDecryptFieldAsync(encryptedBytes, "TEXT", item.ClipDataId) is { } decryptedBytes)
                item.Data = Encoding.UTF8.GetString(decryptedBytes);
        }

        foreach (var item in decryptedJpg)
        {
            if (await TryDecryptFieldAsync(item.Data, "JPG", item.ClipDataId) is { } decryptedBytes)
                item.Data = decryptedBytes;
        }

        foreach (var item in decryptedPng)
        {
            if (await TryDecryptFieldAsync(item.Data, "PNG", item.ClipDataId) is { } decryptedBytes)
                item.Data = decryptedBytes;
        }

        foreach (var item in decryptedBinary)
        {
            if (await TryDecryptFieldAsync(item.Data, "binary", item.ClipDataId) is { } decryptedBytes)
                item.Data = decryptedBytes;
        }

        // Decrypt Title (if it was encrypted)
        var decryptedTitle = clip.Title;
        if (!clip.CustomTitle && TitleLooksEncrypted(clip.Title, metadata, out var encryptedTitleBytes))
        {
            if (await TryDecryptFieldAsync(encryptedTitleBytes, "title", clip.Id) is { } decryptedTitleBytes)
                decryptedTitle = Encoding.UTF8.GetString(decryptedTitleBytes);
            else
                decryptedTitle = "This clip is encrypted - unable to display.";
        }
        else if (!clip.CustomTitle && !string.IsNullOrWhiteSpace(clip.Title))
            _logger.LogDebug("Title for clip {ClipId} does not look encrypted (length: {Length}), keeping as-is", clip.Id, clip.Title.Length);

        if (failedFields > 0 && decryptedFields == 0)
            throw new CryptographicException("The clip could not be decrypted. Incorrect encryption key or corrupted data.");

        clip.Title = decryptedTitle;

        // For temporary decryption, cache the decrypted BLOBs and set in-memory flag
        if (!isPermanent)
        {
            // Cache decrypted BLOB data
            var decryptedData = new DecryptedBlobData(
                decryptedText.AsReadOnly(),
                decryptedJpg.AsReadOnly(),
                decryptedPng.AsReadOnly(),
                decryptedBinary.AsReadOnly()
            );

            _logger.LogDebug("Decrypted {Count} BLOB entities for viewing (no persistence)",
                decryptedText.Count + decryptedJpg.Count + decryptedPng.Count + decryptedBinary.Count);

            // Get expiration from encryption key (set by user in dialog)
            var expiration = encryptionKey.ExpirationMinutes.HasValue
                ? TimeSpan.FromMinutes(encryptionKey.ExpirationMinutes.Value)
                : TimeSpan.MaxValue; // "Until shutdown"

            // Cache with eviction callback to clear IsDecrypted flag when expired
            _blobCacheService.CacheDecryptedBlobs(clip.Id, decryptedData, expiration, OnCacheExpired);

            // Mark as decrypted in-memory (for ClipList icon/title display)
            clip.IsDecrypted = true;
            _logger.LogInformation("Temporarily decrypted clip {ClipId} for viewing (cached for {Minutes} minutes)",
                clip.Id, expiration == TimeSpan.MaxValue
                    ? "until shutdown"
                    : expiration.TotalMinutes.ToString("F0"));
        }
        else
        {
            // Every field decrypted: write the plain text back and clear the encryption fields
            CopyData(decryptedText, textBlobs, (target, source) => target.Data = source.Data);
            CopyData(decryptedJpg, jpgBlobs, (target, source) => target.Data = source.Data);
            CopyData(decryptedPng, pngBlobs, (target, source) => target.Data = source.Data);
            CopyData(decryptedBinary, binaryBlobs, (target, source) => target.Data = source.Data);

            clip.Encrypted = false;
            clip.IsDecrypted = false;
            clip.EncryptionSalt = null;
            clip.EncryptionIv = null;
            clip.EncryptionMethod = null;

            foreach (var item in textBlobs)
                await blobRepository!.UpdateTextAsync(item, cancellationToken);

            foreach (var item in jpgBlobs)
                await blobRepository!.UpdateJpgAsync(item, cancellationToken);

            foreach (var item in pngBlobs)
                await blobRepository!.UpdatePngAsync(item, cancellationToken);

            foreach (var item in binaryBlobs)
                await blobRepository!.UpdateBlobAsync(item, cancellationToken);

            // Encryption cleared the content hash and checksum (they would reveal the plain text); restore them now
            // that the content is plain again
            try
            {
                // Hashed from a scratch clip, so the stored clip's transient content stays unloaded
                var hashSource = new Clip { Id = clip.Id };
                await LoadBlobDataAsync(databaseKey, hashSource, cancellationToken);
                clip.ContentHash = ContentHasher.HashClip(hashSource);
                clip.Checksum = ContentHasher.Checksum(clip.ContentHash);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The clip is decrypted either way; only duplicate detection against it is affected
                _logger.LogWarning(ex, "Could not restore the content hash of decrypted clip {ClipId}", clip.Id);
            }

            await clipRepository!.UpdateAsync(clip, cancellationToken);
            _logger.LogInformation("Permanently decrypted clip {ClipId} - cleared encryption flags and saved decrypted content", clip.Id);
        }

        return true;
    }

    /// <summary>
    /// Whether a clip's (non-custom) title holds ciphertext in <paramref name="metadata" />'s format rather than
    /// plain text, judged by its Base64 length and, for AES-GCM, its version byte.
    /// </summary>
    private static bool TitleLooksEncrypted(string? title, EncryptionMetadata metadata, out byte[] encryptedBytes)
    {
        encryptedBytes = [];
        if (string.IsNullOrWhiteSpace(title) || !RegexPatterns.IsBase64().IsMatch(title))
            return false;

        try
        {
            encryptedBytes = Convert.FromBase64String(title);
        }
        catch (FormatException)
        {
            return false;
        }

        // AES-GCM: version, nonce and tag around at least one byte (an empty title is never encrypted). Legacy CBC: an
        // encrypted checksum block followed by at least one data block.
        return metadata.IsAuthenticated
            ? encryptedBytes.Length > EncryptionMetadata.AesGcmOverhead && encryptedBytes[0] == EncryptionMetadata.AesGcmFormatVersion
            : encryptedBytes.Length >= 32 && encryptedBytes.Length % 16 == 0;
    }

    private static void CopyData<T>(IReadOnlyList<T> sources, IReadOnlyList<T> targets, Action<T, T> copy)
    {
        for (var i = 0; i < targets.Count; i++)
            copy(targets[i], sources[i]);
    }

    /// <summary>
    /// Called when a cached decrypted clip expires.
    /// Sends a message to notify ClipListViewModel to update the UI (clear IsDecrypted flag, change icon).
    /// </summary>
    private void OnCacheExpired(Guid clipId)
    {
        _logger.LogInformation("Cache expired for clip {ClipId} - notifying UI to re-lock", clipId);

        // Send message to ClipListViewModel to update IsDecrypted flag and icon
        _messenger.Send(new ClipCacheExpiredMessage(clipId));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Guid>> LockClipsAsync(string databaseKey, IReadOnlyList<Guid>? clipIds = null, CancellationToken cancellationToken = default)
    {
        // Get all currently cached (temporarily decrypted) clip IDs
        var cachedClipIds = _blobCacheService.GetAllCachedClipIds();

        // If specific clipIds provided, filter to only those that are actually cached
        var clipsToLock = clipIds is { Count: > 0 }
            ? cachedClipIds.Where(clipIds.Contains).ToList()
            : cachedClipIds.ToList();

        var lockedClipIds = new List<Guid>();

        // Clear each cached clip - note that ClearClip doesn't invoke the expiration callback,
        // so the coordinator will need to send ClipCacheExpiredMessage for each locked clip
        foreach (var item in clipsToLock)
        {
            try
            {
                _blobCacheService.ClearClip(item);
                lockedClipIds.Add(item);
                _logger.LogInformation("Locked clip {ClipId}", item);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error locking clip {ClipId}", item);
            }
        }

        return Task.FromResult<IReadOnlyList<Guid>>(lockedClipIds);
    }

    /// <inheritdoc />
    public async Task LoadEncryptedContentAsync(string databaseKey, Clip clip, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clip);

        // Only load if clip is encrypted and NOT already decrypted (decrypted content comes from cache)
        if (!clip.Encrypted || clip.IsDecrypted)
        {
            _logger.LogDebug("Clip {ClipId} is not encrypted or already decrypted, skipping encrypted content load", clip.Id);
            return;
        }

        var blobRepository = _databaseContextFactory.GetBlobRepository(databaseKey);

        // Load encrypted base64 data from BLOB tables into transient properties
        switch (clip.Type)
        {
            case ClipType.Text:
            case ClipType.Html:
            case ClipType.RichText:
                var textBlobs = await blobRepository.GetTextByClipIdAsync(clip.Id, cancellationToken);
                if (textBlobs.Count > 0)
                {
                    // Set encrypted base64 data into TextContent
                    clip.TextContent = textBlobs[0].Data;
                    _logger.LogDebug("Loaded encrypted text content (base64) for clip {ClipId}", clip.Id);
                }

                break;

            case ClipType.Image:
                // Try PNG first, then JPG
                var pngBlobs = await blobRepository.GetPngByClipIdAsync(clip.Id, cancellationToken);
                if (pngBlobs.Count > 0)
                {
                    clip.ImageData = pngBlobs[0].Data;
                    _logger.LogDebug("Loaded encrypted PNG content for clip {ClipId}", clip.Id);
                }
                else
                {
                    var jpgBlobs = await blobRepository.GetJpgByClipIdAsync(clip.Id, cancellationToken);
                    if (jpgBlobs.Count > 0)
                    {
                        clip.ImageData = jpgBlobs[0].Data;
                        _logger.LogDebug("Loaded encrypted JPG content for clip {ClipId}", clip.Id);
                    }
                }

                break;

            case ClipType.Files:
                // Files typically stored as binary BLOBs
                var binaryBlobs = await blobRepository.GetBlobByClipIdAsync(clip.Id, cancellationToken);
                if (binaryBlobs.Count > 0)
                {
                    // Store in TextContent as base64 for file paths JSON
                    var textBlobsForFiles = await blobRepository.GetTextByClipIdAsync(clip.Id, cancellationToken);
                    if (textBlobsForFiles.Count > 0)
                    {
                        clip.FilePathsJson = textBlobsForFiles[0].Data;
                        _logger.LogDebug("Loaded encrypted file paths for clip {ClipId}", clip.Id);
                    }
                }

                break;
        }
    }

    #endregion
}
