using ClipMate.Core.Models;

namespace ClipMate.Core.Repositories;

/// <summary>
/// Repository interface for managing Monaco Editor state persistence.
/// </summary>
public interface IMonacoEditorStateRepository
{
    /// <summary>
    /// Gets the Monaco Editor state for a specific ClipData.
    /// </summary>
    /// <param name="clipDataId">The ClipData ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The Monaco Editor state, or null if not found.</returns>
    Task<MonacoEditorState?> GetByClipDataIdAsync(Guid clipDataId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates Monaco Editor state for a ClipData.
    /// </summary>
    /// <param name="state">The state to upsert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpsertAsync(MonacoEditorState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes Monaco Editor state for a specific ClipData.
    /// </summary>
    /// <param name="clipDataId">The ClipData ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteByClipDataIdAsync(Guid clipDataId, CancellationToken cancellationToken = default);
}
