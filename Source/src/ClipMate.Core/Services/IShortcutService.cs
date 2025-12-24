using ClipMate.Core.Models;

namespace ClipMate.Core.Services;

/// <summary>
/// Service for managing clip shortcuts (nicknames for PowerPaste).
/// </summary>
public interface IShortcutService
{
    /// <summary>
    /// Gets all shortcuts from the specified database.
    /// </summary>
    /// <param name="databaseKey">The database key (path).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all shortcuts.</returns>
    Task<IReadOnlyList<Shortcut>> GetAllAsync(string databaseKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all shortcuts from all loaded databases.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of shortcuts with their database keys.</returns>
    Task<IReadOnlyList<(string DatabaseKey, Shortcut Shortcut)>> GetAllFromAllDatabasesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets shortcuts matching the specified nickname prefix from the specified database.
    /// </summary>
    /// <param name="databaseKey">The database key (path).</param>
    /// <param name="nicknamePrefix">The nickname prefix to match (case-insensitive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of matching shortcuts.</returns>
    Task<IReadOnlyList<Shortcut>> GetByNicknamePrefixAsync(string databaseKey, string nicknamePrefix, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets shortcuts matching the specified nickname prefix from all loaded databases.
    /// </summary>
    /// <param name="nicknamePrefix">The nickname prefix to match (case-insensitive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of matching shortcuts with their database keys.</returns>
    Task<IReadOnlyList<(string DatabaseKey, Shortcut Shortcut)>> GetByNicknamePrefixFromAllDatabasesAsync(string nicknamePrefix, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the shortcut for a specific clip from the specified database.
    /// </summary>
    /// <param name="databaseKey">The database key (path).</param>
    /// <param name="clipId">The clip ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The shortcut if found; otherwise, null.</returns>
    Task<Shortcut?> GetByClipIdAsync(string databaseKey, Guid clipId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates or creates a shortcut for a clip. If nickname is null/empty, deletes the shortcut.
    /// </summary>
    /// <param name="databaseKey">The database key (path).</param>
    /// <param name="clipId">The clip ID.</param>
    /// <param name="nickname">The shortcut nickname (max 64 chars), or null to delete.</param>
    /// <param name="title">The clip title to update, or null to leave unchanged.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateClipShortcutAsync(string databaseKey, Guid clipId, string? nickname, string? title = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a shortcut by ID from the specified database.
    /// </summary>
    /// <param name="databaseKey">The database key (path).</param>
    /// <param name="shortcutId">The shortcut ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(string databaseKey, Guid shortcutId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all shortcuts for a specific clip from the specified database.
    /// </summary>
    /// <param name="databaseKey">The database key (path).</param>
    /// <param name="clipId">The clip ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteByClipIdAsync(string databaseKey, Guid clipId, CancellationToken cancellationToken = default);
}
