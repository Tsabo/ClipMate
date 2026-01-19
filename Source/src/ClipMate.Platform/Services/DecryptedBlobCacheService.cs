using System.Collections.Concurrent;
using ClipMate.Core.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ClipMate.Platform.Services;

/// <summary>
/// Implementation of IDecryptedBlobCacheService using IMemoryCache.
/// Provides automatic expiration with reliable timer-based callbacks.
/// </summary>
public class DecryptedBlobCacheService : IDecryptedBlobCacheService, IDisposable
{
    private static readonly TimeSpan _defaultExpiration = TimeSpan.FromMinutes(15);
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _expirationTimers;
    private readonly ILogger<DecryptedBlobCacheService> _logger;
    private bool _disposed;

    public DecryptedBlobCacheService(IMemoryCache cache, ILogger<DecryptedBlobCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
        _expirationTimers = new ConcurrentDictionary<Guid, CancellationTokenSource>();
    }

    public void CacheDecryptedBlobs(Guid clipId, DecryptedBlobData blobs, TimeSpan? expiration = null, Action<Guid>? onExpired = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var expirationTime = expiration ?? _defaultExpiration;

        var cacheOptions = new MemoryCacheEntryOptions();

        // Handle "until shutdown" case (TimeSpan.MaxValue or very long duration)
        if (expirationTime == TimeSpan.MaxValue || expirationTime.TotalDays > 365)
        {
            cacheOptions.SetPriority(CacheItemPriority.NeverRemove);
            _logger.LogInformation("Caching decrypted BLOBs for clip {ClipId} until shutdown", clipId);

            // Still track in _expirationTimers with a non-expiring token for GetAllCachedClipIds()
            CancelExpirationTimer(clipId);
            var cts = new CancellationTokenSource();
            _expirationTimers[clipId] = cts;
        }
        else
        {
            // Use sliding expiration for cache, but timer for callback
            cacheOptions.SetSlidingExpiration(expirationTime);
            _logger.LogInformation("Caching decrypted BLOBs for clip {ClipId} for {Minutes} minutes",
                clipId, expirationTime.TotalMinutes);

            // Set up timer-based expiration callback for reliable timing
            CancelExpirationTimer(clipId);
            var cts = new CancellationTokenSource();
            _expirationTimers[clipId] = cts;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(expirationTime, cts.Token);

                    // Timer fired - remove from cache and notify
                    _cache.Remove(GetCacheKey(clipId));
                    CancelExpirationTimer(clipId);

                    _logger.LogInformation("Decrypted BLOB cache expired for clip {ClipId}", clipId);
                    onExpired?.Invoke(clipId);
                }
                catch (OperationCanceledException)
                {
                    // Timer was cancelled (manual clear or replaced)
                }
            }, cts.Token);
        }

        // Still register IMemoryCache callback for eviction scenarios (memory pressure, etc.)
        cacheOptions.RegisterPostEvictionCallback((key, _, reason, _) =>
        {
            if (reason != EvictionReason.Removed) // Removed = manual, already handled by timer
            {
                // Extract Guid from cache key string (format: "DecryptedBlobs_{guid}")
                var keyString = key.ToString();
                if (keyString == null || !keyString.StartsWith("DecryptedBlobs_") ||
                    !Guid.TryParse(keyString["DecryptedBlobs_".Length..], out var evictedClipId))
                    return;

                CancelExpirationTimer(evictedClipId);

                _logger.LogInformation("Decrypted BLOB cache evicted for clip {ClipId}, reason: {Reason}",
                    evictedClipId, reason);

                onExpired?.Invoke(evictedClipId);
            }
        });

        _cache.Set(GetCacheKey(clipId), blobs, cacheOptions);
    }

    public DecryptedBlobData? GetDecryptedBlobs(Guid clipId)
    {
        var cached = _cache.Get<DecryptedBlobData>(GetCacheKey(clipId));
        if (cached != null)
            _logger.LogDebug("Cache hit for clip {ClipId}", clipId);

        return cached;
    }

    public bool IsClipDecrypted(Guid clipId) => _cache.TryGetValue(GetCacheKey(clipId), out var _);

    public void ClearClip(Guid clipId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        CancelExpirationTimer(clipId);
        _cache.Remove(GetCacheKey(clipId));
        _logger.LogInformation("Cleared decrypted BLOB cache for clip {ClipId}", clipId);
    }

    public IReadOnlyList<Guid> GetAllCachedClipIds()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _expirationTimers.Keys.ToList();
    }

    public void ClearAll()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Cancel all timers
        foreach (var item in _expirationTimers.Keys.ToList())
            CancelExpirationTimer(item);

        _logger.LogInformation("ClearAll requested - all expiration timers cancelled");
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Cancel and dispose all timers
        foreach (var item in _expirationTimers.Values)
        {
            item.Cancel();
            item.Dispose();
        }

        _expirationTimers.Clear();
    }

    private void CancelExpirationTimer(Guid clipId)
    {
        if (!_expirationTimers.TryRemove(clipId, out var cts))
            return;

        cts.Cancel();
        cts.Dispose();
    }

    private static string GetCacheKey(Guid clipId) => $"DecryptedBlobs_{clipId}";
}
