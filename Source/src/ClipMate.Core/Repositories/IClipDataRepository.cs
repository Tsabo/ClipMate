using ClipMate.Core.Models;

namespace ClipMate.Core.Repositories;

/// <summary>
/// Repository interface for managing ClipData entities (clipboard format metadata).
/// </summary>
public interface IClipDataRepository
{
    /// <summary>
    /// Retrieves all format metadata for a specific clip.
    /// </summary>
    /// <param name="clipId">The clip's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of format metadata entries.</returns>
    Task<IReadOnlyList<ClipData>> GetByClipIdAsync(Guid clipId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific format metadata entry by ID.
    /// </summary>
    /// <param name="id">The ClipData's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The ClipData if found; otherwise, null.</returns>
    Task<ClipData?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new ClipData entry.
    /// </summary>
    /// <param name="clipData">The ClipData to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created ClipData with generated ID.</returns>
    Task<ClipData> CreateAsync(ClipData clipData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates multiple ClipData entries in a single operation.
    /// </summary>
    /// <param name="clipDataList">List of ClipData to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CreateRangeAsync(IEnumerable<ClipData> clipDataList, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all ClipData entries for a specific clip.
    /// </summary>
    /// <param name="clipId">The clip's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of entries deleted.</returns>
    Task<int> DeleteByClipIdAsync(Guid clipId, CancellationToken cancellationToken = default);
}
