using ClipMate.Core.Models;

namespace ClipMate.Core.Repositories;

/// <summary>
/// Repository interface for managing ApplicationFilter entities in the data store.
/// </summary>
public interface IApplicationFilterRepository
{
    /// <summary>
    /// Retrieves an application filter by its unique identifier.
    /// </summary>
    /// <param name="id">The filter's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The filter if found; otherwise, null.</returns>
    Task<ApplicationFilter?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all enabled application filters.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of enabled filters.</returns>
    Task<IReadOnlyList<ApplicationFilter>> GetEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all application filters.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all filters.</returns>
    Task<IReadOnlyList<ApplicationFilter>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new application filter in the data store.
    /// </summary>
    /// <param name="filter">The filter to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created filter with generated ID.</returns>
    Task<ApplicationFilter> CreateAsync(ApplicationFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing application filter in the data store.
    /// </summary>
    /// <param name="filter">The filter to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if updated successfully; otherwise, false.</returns>
    Task<bool> UpdateAsync(ApplicationFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an application filter from the data store.
    /// </summary>
    /// <param name="id">The filter's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted successfully; otherwise, false.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
