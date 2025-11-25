using ClipMate.Core.Models;

namespace ClipMate.Core.Repositories;

/// <summary>
/// Repository interface for managing Shortcut entities (PowerPaste nicknames).
/// </summary>
public interface IShortcutRepository
{
    /// <summary>
    /// Retrieves a shortcut by its unique identifier.
    /// </summary>
    /// <param name="id">The shortcut's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The shortcut if found; otherwise, null.</returns>
    Task<Shortcut?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a shortcut by its nickname.
    /// </summary>
    /// <param name="nickname">The shortcut nickname (e.g., ".sig", ".addr").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The shortcut if found; otherwise, null.</returns>
    Task<Shortcut?> GetByNicknameAsync(string nickname, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all shortcuts for a specific clip.
    /// </summary>
    /// <param name="clipId">The clip's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of shortcuts for the clip.</returns>
    Task<IReadOnlyList<Shortcut>> GetByClipIdAsync(Guid clipId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all shortcuts, ordered by nickname.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all shortcuts.</returns>
    Task<IReadOnlyList<Shortcut>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new shortcut.
    /// </summary>
    /// <param name="shortcut">The shortcut to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created shortcut with generated ID.</returns>
    Task<Shortcut> CreateAsync(Shortcut shortcut, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing shortcut.
    /// </summary>
    /// <param name="shortcut">The shortcut to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if updated successfully; otherwise, false.</returns>
    Task<bool> UpdateAsync(Shortcut shortcut, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a shortcut.
    /// </summary>
    /// <param name="id">The shortcut's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted successfully; otherwise, false.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all shortcuts for a specific clip.
    /// </summary>
    /// <param name="clipId">The clip's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of shortcuts deleted.</returns>
    Task<int> DeleteByClipIdAsync(Guid clipId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a nickname is already in use.
    /// </summary>
    /// <param name="nickname">The nickname to check.</param>
    /// <param name="excludeId">Optional shortcut ID to exclude from check (for updates).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if nickname exists; otherwise, false.</returns>
    Task<bool> NicknameExistsAsync(string nickname, Guid? excludeId = null, CancellationToken cancellationToken = default);
}
