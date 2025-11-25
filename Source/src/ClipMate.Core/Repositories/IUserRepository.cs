using ClipMate.Core.Models;

namespace ClipMate.Core.Repositories;

/// <summary>
/// Repository interface for managing User entities.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Retrieves a user by their unique identifier.
    /// </summary>
    /// <param name="id">The user's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user if found; otherwise, null.</returns>
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a user by username and workstation.
    /// </summary>
    /// <param name="username">The username.</param>
    /// <param name="workstation">The workstation name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user if found; otherwise, null.</returns>
    Task<User?> GetByUsernameAndWorkstationAsync(string username, string workstation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all users.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all users.</returns>
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new user or updates last activity if they already exist.
    /// </summary>
    /// <param name="user">The user to create or update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created or updated user.</returns>
    Task<User> CreateOrUpdateAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a user's last activity timestamp.
    /// </summary>
    /// <param name="id">The user's unique identifier.</param>
    /// <param name="lastDate">The new last activity timestamp.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if updated successfully; otherwise, false.</returns>
    Task<bool> UpdateLastActivityAsync(Guid id, DateTime lastDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a user.
    /// </summary>
    /// <param name="id">The user's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted successfully; otherwise, false.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
