using ClipMate.Core.Models;

namespace ClipMate.Core.Services;

/// <summary>
/// Service for caching decrypted BLOB data in memory.
/// Supports per-clip caching with automatic expiration and memory pressure handling.
/// </summary>
public interface IDecryptedBlobCacheService
{
    /// <summary>
    /// Cache decrypted BLOB data for a clip.
    /// </summary>
    /// <param name="clipId">The clip ID</param>
    /// <param name="blobs">The decrypted BLOB data</param>
    /// <param name="expiration">Optional expiration time. If null, uses default. Use TimeSpan.MaxValue for "until shutdown".</param>
    /// <param name="onExpired">Optional callback invoked when cache entry expires or is evicted</param>
    void CacheDecryptedBlobs(Guid clipId, DecryptedBlobData blobs, TimeSpan? expiration = null, Action<Guid>? onExpired = null);

    /// <summary>
    /// Retrieve cached decrypted BLOB data for a clip.
    /// </summary>
    /// <param name="clipId">The clip ID</param>
    /// <returns>Decrypted BLOB data if cached, null otherwise</returns>
    DecryptedBlobData? GetDecryptedBlobs(Guid clipId);

    /// <summary>
    /// Check if a clip has cached decrypted data.
    /// </summary>
    /// <param name="clipId">The clip ID</param>
    /// <returns>True if decrypted data is cached</returns>
    bool IsClipDecrypted(Guid clipId);

    /// <summary>
    /// Remove cached data for a specific clip.
    /// </summary>
    /// <param name="clipId">The clip ID</param>
    void ClearClip(Guid clipId);

    /// <summary>
    /// Get all cached clip IDs.
    /// </summary>
    /// <returns>Collection of clip IDs that have cached decrypted data</returns>
    IReadOnlyList<Guid> GetAllCachedClipIds();

    /// <summary>
    /// Clear all cached decrypted data.
    /// </summary>
    void ClearAll();
}

/// <summary>
/// Container for all decrypted BLOB data for a clip.
/// </summary>
/// <param name="TextBlobs">Text BLOBs with decrypted Data property</param>
/// <param name="JpgBlobs">JPG BLOBs with decrypted Data property</param>
/// <param name="PngBlobs">PNG BLOBs with decrypted Data property</param>
/// <param name="BinaryBlobs">Binary BLOBs with decrypted Data property</param>
public record DecryptedBlobData(
    IReadOnlyList<BlobTxt> TextBlobs,
    IReadOnlyList<BlobJpg> JpgBlobs,
    IReadOnlyList<BlobPng> PngBlobs,
    IReadOnlyList<BlobBlob> BinaryBlobs);
