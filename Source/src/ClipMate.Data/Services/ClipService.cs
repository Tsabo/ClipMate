using System.Text;
using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using ClipMate.Platform.Helpers;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Service for managing clips (CRUD operations, history management).
/// Registered as singleton to support multi-database operations via repository factories.
/// </summary>
public class ClipService : IClipService
{
    private readonly IClipboardService _clipboardService;
    private readonly IConfigurationService _configurationService;
    private readonly IDatabaseContextFactory _databaseContextFactory;
    private readonly ILogger<ClipService> _logger;
    private readonly ITemplateService _templateService;

    public ClipService(IDatabaseContextFactory databaseContextFactory,
        IConfigurationService configurationService,
        IClipboardService clipboardService,
        ITemplateService templateService,
        ILogger<ClipService> logger)
    {
        _databaseContextFactory = databaseContextFactory ?? throw new ArgumentNullException(nameof(databaseContextFactory));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
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
    public async Task<IReadOnlyList<string>> GetDistinctFormatsAsync(string databaseKey, CancellationToken cancellationToken = default)
    {
        var repository = _databaseContextFactory.GetClipDataRepository(databaseKey);
        return await repository.GetDistinctFormatsAsync(cancellationToken);
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
        var targetRepository = GetRepository(targetDatabaseKey);
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

        var createdClip = await targetRepository.CreateAsync(copiedClip, cancellationToken);

        // Copy all ClipData formats and associated BLOB data from source database to target database
        await CopyClipDataAndBlobsAsync(sourceDatabaseKey, sourceClipId, targetDatabaseKey, createdClip.Id, cancellationToken);

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

    /// <inheritdoc />
    public async Task LoadAndSetClipboardAsync(string databaseKey, Guid clipId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get the clip
            var repository = GetRepository(databaseKey);
            var clip = await repository.GetByIdAsync(clipId, cancellationToken);
            if (clip == null)
            {
                _logger.LogWarning("Clip {ClipId} not found in database {DatabaseKey}", clipId, databaseKey);
                return;
            }

            // Create repositories to load full clip content
            var clipDataRepository = _databaseContextFactory.GetClipDataRepository(databaseKey);
            var blobRepository = _databaseContextFactory.GetBlobRepository(databaseKey);

            // Load ClipData for this clip
            var clipDataList = await clipDataRepository.GetByClipIdAsync(clip.Id, cancellationToken);
            if (clipDataList.Count == 0)
            {
                _logger.LogWarning("No ClipData found for clip: {ClipId}", clip.Id);
                return;
            }

            // Load text blobs and populate content properties
            var textBlobs = await blobRepository.GetTextByClipIdAsync(clip.Id, cancellationToken);
            var textBlobsDict = textBlobs.ToDictionary(p => p.ClipDataId);

            foreach (var item in clipDataList)
            {
                if (!textBlobsDict.TryGetValue(item.Id, out var textBlob))
                    continue;

                // Determine content type based on format
                if (item.Format == Formats.Text.Code || item.Format == Formats.UnicodeText.Code)
                    clip.TextContent = textBlob.Data;
                else if (item.Format == Formats.RichText.Code)
                    clip.RtfContent = textBlob.Data;
                else if (item.Format == Formats.Html.Code || item.Format == Formats.HtmlAlt.Code)
                    clip.HtmlContent = textBlob.Data;
            }

            // For images, load image data
            if (clip.Type == ClipType.Image)
            {
                var pngBlobs = await blobRepository.GetPngByClipIdAsync(clip.Id, cancellationToken);
                if (pngBlobs.Count > 0)
                    clip.ImageData = pngBlobs[0].Data;
                else
                {
                    var jpgBlobs = await blobRepository.GetJpgByClipIdAsync(clip.Id, cancellationToken);
                    if (jpgBlobs.Count > 0)
                        clip.ImageData = jpgBlobs[0].Data;
                }
            }

            // For files, load file paths from binary blobs
            if (clip.Type == ClipType.Files)
            {
                var binaryBlobs = await blobRepository.GetBlobByClipIdAsync(clip.Id, cancellationToken);
                var filePathBlob = binaryBlobs.FirstOrDefault(b =>
                    clipDataList.Any(p => p.Id == b.ClipDataId && p.Format == Formats.HDrop.Code));

                if (filePathBlob != null)
                    clip.FilePathsJson = Encoding.UTF8.GetString(filePathBlob.Data);
            }

            // Apply template transformation if active
            var transformedClip = _templateService.TryApplyTemplate(clip);
            if (transformedClip != null)
                clip = transformedClip;

            // Generate ContentHash for suppression (prevent re-capturing this clip)
            // IMPORTANT: Must compute hash AFTER loading content from database
            clip.ContentHash = clip.Type switch
            {
                ClipType.Text or ClipType.RichText or ClipType.Html => ContentHasher.HashText(clip.TextContent ?? string.Empty),
                ClipType.Image => ContentHasher.HashBytes(clip.ImageData ?? []),
                ClipType.Files => ContentHasher.HashText(clip.FilePathsJson ?? string.Empty),
                var _ => string.Empty,
            };

            // Update the Windows clipboard
            await _clipboardService.SetClipboardContentAsync(clip, cancellationToken);
            _logger.LogInformation("Set clipboard content for clip: {ClipId} from database {DatabaseKey}", clip.Id, databaseKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load and set clipboard for clip {ClipId} from database {DatabaseKey}", clipId, databaseKey);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task LoadBlobDataAsync(string databaseKey, Clip clip, CancellationToken cancellationToken = default)
    {
        try
        {
            // Create repositories to load full clip content
            var clipDataRepository = _databaseContextFactory.GetClipDataRepository(databaseKey);
            var blobRepository = _databaseContextFactory.GetBlobRepository(databaseKey);

            // Load ClipData for this clip
            var clipDataList = await clipDataRepository.GetByClipIdAsync(clip.Id, cancellationToken);
            if (clipDataList.Count == 0)
            {
                _logger.LogDebug("No ClipData found for clip: {ClipId}", clip.Id);
                return;
            }

            // Load text blobs and populate content properties
            var textBlobs = await blobRepository.GetTextByClipIdAsync(clip.Id, cancellationToken);
            var textBlobsDict = textBlobs.ToDictionary(p => p.ClipDataId);

            foreach (var item in clipDataList)
            {
                if (!textBlobsDict.TryGetValue(item.Id, out var textBlob))
                    continue;

                // Determine content type based on format
                if (item.Format == Formats.Text.Code || item.Format == Formats.UnicodeText.Code)
                    clip.TextContent = textBlob.Data;
                else if (item.Format == Formats.RichText.Code)
                    clip.RtfContent = textBlob.Data;
                else if (item.Format == Formats.Html.Code || item.Format == Formats.HtmlAlt.Code)
                    clip.HtmlContent = textBlob.Data;
            }

            // For images, load image data
            if (clip.Type == ClipType.Image)
            {
                var pngBlobs = await blobRepository.GetPngByClipIdAsync(clip.Id, cancellationToken);
                if (pngBlobs.Count > 0)
                    clip.ImageData = pngBlobs[0].Data;
                else
                {
                    var jpgBlobs = await blobRepository.GetJpgByClipIdAsync(clip.Id, cancellationToken);
                    if (jpgBlobs.Count > 0)
                        clip.ImageData = jpgBlobs[0].Data;
                }
            }

            // For files, load file paths from binary blobs
            if (clip.Type == ClipType.Files)
            {
                var binaryBlobs = await blobRepository.GetBlobByClipIdAsync(clip.Id, cancellationToken);
                var filePathBlob = binaryBlobs.FirstOrDefault(b =>
                    clipDataList.Any(p => p.Id == b.ClipDataId && p.Format == Formats.HDrop.Code));

                if (filePathBlob != null)
                    clip.FilePathsJson = Encoding.UTF8.GetString(filePathBlob.Data);
            }

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
        var processedQuery = ReplaceSqlPlaceholders(sqlQuery, retentionLimit);

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
    public async Task<IReadOnlyList<Clip>> GetByCollectionRecursiveAsync(string databaseKey, Guid collectionId, bool includeDeleted = false, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting clips recursively for collection {CollectionId} in database {DatabaseKey}", collectionId, databaseKey);

        var repository = GetRepository(databaseKey);
        var folderRepository = _databaseContextFactory.GetFolderRepository(databaseKey);

        // Get clips directly in the collection
        var collectionClips = await repository.GetByCollectionAsync(collectionId, cancellationToken);

        // Get all folders in this collection recursively
        var allFolders = await folderRepository.GetByCollectionAsync(collectionId, cancellationToken);

        // Get clips from each folder
        var folderClips = new List<Clip>();
        foreach (var item in allFolders)
        {
            var clips = await repository.GetByFolderAsync(item.Id, cancellationToken);
            folderClips.AddRange(clips);
        }

        // Combine and deduplicate (a clip shouldn't be in multiple places, but just in case)
        var allClips = collectionClips.Concat(folderClips)
            .GroupBy(p => p.Id)
            .Select(p => p.First())
            .ToList();

        // Filter by deleted status if needed
        if (!includeDeleted)
            allClips = allClips.Where(p => !p.Del).ToList();

        _logger.LogInformation("Found {Count} clips recursively in collection {CollectionId}", allClips.Count, collectionId);

        return allClips.AsReadOnly();
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
        var storageTypes = sourceClipFormats.Select(f => f.StorageType).Distinct();

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
    /// Replaces SQL query placeholders with actual values.
    /// Supported placeholders:
    /// - #DATE# - Today's date at midnight (YYYY-MM-DD HH:MM:SS)
    /// - #DATEMINUSLIMIT# - Date minus retention limit days
    /// - #DATELASTIMPORT# - Date of last import (placeholder for future feature, defaults to 30 days ago)
    /// - #DATELASTEXPORT# - Date of last export (placeholder for future feature, defaults to 30 days ago)
    /// </summary>
    private static string ReplaceSqlPlaceholders(string sqlQuery, int retentionLimit)
    {
        var now = DateTime.Now;
        var today = now.Date; // Midnight today
        var dateMinusLimit = today.AddDays(-retentionLimit);

        // Format dates for SQLite (ISO 8601 format: YYYY-MM-DD HH:MM:SS)
        var todayStr = today.ToString("yyyy-MM-dd HH:mm:ss");
        var dateMinusLimitStr = dateMinusLimit.ToString("yyyy-MM-dd HH:mm:ss");

        // For now, use fixed defaults for import/export dates (feature not yet implemented)
        var lastImportStr = today.AddDays(-30).ToString("yyyy-MM-dd HH:mm:ss");
        var lastExportStr = today.AddDays(-30).ToString("yyyy-MM-dd HH:mm:ss");

        // Replace all placeholders
        var result = sqlQuery
            .Replace("#DATE#", todayStr, StringComparison.OrdinalIgnoreCase)
            .Replace("#DATEMINUSLIMIT#", dateMinusLimitStr, StringComparison.OrdinalIgnoreCase)
            .Replace("#DATELASTIMPORT#", lastImportStr, StringComparison.OrdinalIgnoreCase)
            .Replace("#DATELASTEXPORT#", lastExportStr, StringComparison.OrdinalIgnoreCase);

        return result;
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
}
