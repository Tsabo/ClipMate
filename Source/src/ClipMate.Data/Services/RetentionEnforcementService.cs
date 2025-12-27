using ClipMate.Core.Models;
using ClipMate.Core.Services;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Service for enforcing retention rules on collections.
/// Implements InBox → Overflow → Trashcan retention logic.
/// </summary>
public class RetentionEnforcementService : IRetentionEnforcementService
{
    private readonly IDatabaseContextFactory _contextFactory;
    private readonly ILogger<IRetentionEnforcementService> _logger;

    public RetentionEnforcementService(IDatabaseContextFactory contextFactory,
        ILogger<IRetentionEnforcementService> logger)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<int> EnforceRetentionAsync(string databaseKey, Guid collectionId)
    {
        _logger.LogDebug("Enforcing retention for collection {CollectionId} in database {DatabaseKey}", collectionId, databaseKey);

        var clipRepository = _contextFactory.GetClipRepository(databaseKey);
        var collectionRepository = _contextFactory.GetCollectionRepository(databaseKey);

        // Load collection with retention settings
        var collection = await collectionRepository.GetByIdAsync(databaseKey, collectionId);
        if (collection == null)
        {
            _logger.LogWarning("Collection {CollectionId} not found", collectionId);
            return 0;
        }

        // Skip if ReadOnly
        if (collection.ReadOnly)
        {
            _logger.LogDebug("Skipping ReadOnly collection: {CollectionName}", collection.Title);
            return 0;
        }

        var clipsProcessed = 0;

        // Get all clips in collection ordered by timestamp (oldest first)
        var allClips = await clipRepository.GetClipsInCollectionAsync(collectionId);
        var clips = allClips.OrderBy(p => p.CapturedAt).ToList();

        // Enforce MaxAge first (delete old clips)
        if (collection.MaxAgeDays > 0)
        {
            var ageThreshold = DateTimeOffset.UtcNow.AddDays(-collection.MaxAgeDays);
            var oldClips = clips.Where(p => p.CapturedAt < ageThreshold).ToList();

            if (oldClips.Any())
            {
                _logger.LogInformation(
                    "Deleting {Count} clips older than {Days} days from {CollectionName}",
                    oldClips.Count,
                    collection.MaxAgeDays,
                    collection.Title);

                await clipRepository.DeleteClipsAsync(oldClips.Select(c => c.Id));
                clipsProcessed += oldClips.Count;
                clips = clips.Except(oldClips).ToList();
            }
        }

        var clipsToMove = new List<Clip>();

        // Enforce MaxBytes
        if (collection.MaxBytes > 0)
        {
            var totalBytes = clips.Sum(p => (long)p.Size);
            

            while (totalBytes > collection.MaxBytes && clips.Count > 0)
            {
                var oldest = clips.First();
                clipsToMove.Add(oldest);
                totalBytes -= oldest.Size;
                clips.Remove(oldest);
            }

            if (clipsToMove.Count > 0)
                clipsProcessed += await MoveClipsToDestination(databaseKey, collection, clipsToMove);
        }

        // Enforce MaxClips
        if (collection.MaxClips <= 0 || clips.Count <= collection.MaxClips)
            return clipsProcessed;

        var excessCount = clips.Count - collection.MaxClips;
        clipsToMove = clips.Take(excessCount).ToList();

        _logger.LogInformation(
            "Moving {Count} excess clips from {CollectionName} (limit: {MaxClips})",
            excessCount,
            collection.Title,
            collection.MaxClips);

        clipsProcessed += await MoveClipsToDestination(databaseKey, collection, clipsToMove);

        return clipsProcessed;
    }

    /// <inheritdoc />
    public async Task<int> EnforceAllCollectionsAsync(string databaseKey)
    {
        _logger.LogInformation("Enforcing retention across all collections in database {DatabaseKey}", databaseKey);

        var collectionRepository = _contextFactory.GetCollectionRepository(databaseKey);

        // Get all non-ReadOnly collections
        var allCollections = await collectionRepository.GetAllAsync();
        var targetCollections = allCollections
            .Where(p => !p.ReadOnly && (p.MaxClips > 0 || p.MaxBytes > 0 || p.MaxAgeDays > 0))
            .ToList();

        _logger.LogDebug("Found {Count} collections with retention rules", targetCollections.Count);

        var totalProcessed = 0;
        foreach (var collection in targetCollections)
        {
            try
            {
                var processed = await EnforceRetentionAsync(databaseKey, collection.Id);
                totalProcessed += processed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enforcing retention for collection {CollectionId}", collection.Id);
            }
        }

        _logger.LogInformation("Retention enforcement complete. Total clips processed: {Total}", totalProcessed);
        return totalProcessed;
    }

    /// <summary>
    /// Moves clips to Overflow or Trashcan based on collection type.
    /// </summary>
    private async Task<int> MoveClipsToDestination(string databaseKey, Collection sourceCollection, List<Clip> clips)
    {
        if (clips.Count == 0)
            return 0;

        var clipRepository = _contextFactory.GetClipRepository(databaseKey);
        var collectionRepository = _contextFactory.GetCollectionRepository(databaseKey);
        Guid destinationId;

        // Determine destination based on collection type
        if (sourceCollection.Role == CollectionRole.Overflow)
        {
            // Overflow → Trashcan
            var trashcan = await collectionRepository.GetTrashcanCollectionAsync(databaseKey);
            destinationId = trashcan.Id;
            _logger.LogInformation("Moving {Count} clips from Overflow to Trashcan", clips.Count);
        }
        else
        {
            // Normal/InBox → Overflow
            var overflow = await collectionRepository.GetOverflowCollectionAsync(databaseKey);
            destinationId = overflow.Id;
            _logger.LogInformation("Moving {Count} clips to Overflow", clips.Count);
        }

        await clipRepository.MoveClipsToCollectionAsync(clips.Select(p => p.Id), destinationId);
        return clips.Count;
    }
}
