using ClipMate.Core.Models;

namespace ClipMate.Core.Repositories;

/// <summary>
/// Repository interface for managing BLOB storage (BlobTxt, BlobJpg, BlobPng, BlobBlob).
/// Provides unified access to all BLOB table types.
/// </summary>
public interface IBlobRepository
{
    /// <summary>
    /// Stores text content in BlobTxt table.
    /// </summary>
    /// <param name="blobTxt">The text BLOB to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stored BlobTxt with generated ID.</returns>
    Task<BlobTxt> CreateTextAsync(BlobTxt blobTxt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores JPEG image in BlobJpg table.
    /// </summary>
    /// <param name="blobJpg">The JPEG BLOB to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stored BlobJpg with generated ID.</returns>
    Task<BlobJpg> CreateJpgAsync(BlobJpg blobJpg, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores PNG image in BlobPng table.
    /// </summary>
    /// <param name="blobPng">The PNG BLOB to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stored BlobPng with generated ID.</returns>
    Task<BlobPng> CreatePngAsync(BlobPng blobPng, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores generic binary data in BlobBlob table.
    /// </summary>
    /// <param name="blobBlob">The binary BLOB to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stored BlobBlob with generated ID.</returns>
    Task<BlobBlob> CreateBlobAsync(BlobBlob blobBlob, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves text BLOBs for a specific clip.
    /// </summary>
    /// <param name="clipId">The clip's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of text BLOBs.</returns>
    Task<IReadOnlyList<BlobTxt>> GetTextByClipIdAsync(Guid clipId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves JPEG BLOBs for a specific clip.
    /// </summary>
    /// <param name="clipId">The clip's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of JPEG BLOBs.</returns>
    Task<IReadOnlyList<BlobJpg>> GetJpgByClipIdAsync(Guid clipId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves PNG BLOBs for a specific clip.
    /// </summary>
    /// <param name="clipId">The clip's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of PNG BLOBs.</returns>
    Task<IReadOnlyList<BlobPng>> GetPngByClipIdAsync(Guid clipId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves generic binary BLOBs for a specific clip.
    /// </summary>
    /// <param name="clipId">The clip's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of binary BLOBs.</returns>
    Task<IReadOnlyList<BlobBlob>> GetBlobByClipIdAsync(Guid clipId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing text BLOB.
    /// </summary>
    /// <param name="blobTxt">The text BLOB to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateTextAsync(BlobTxt blobTxt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all BLOBs for a specific clip across all BLOB tables.
    /// </summary>
    /// <param name="clipId">The clip's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Total number of BLOBs deleted.</returns>
    Task<int> DeleteByClipIdAsync(Guid clipId, CancellationToken cancellationToken = default);
}
