using ClipMate.Core.Models;

namespace ClipMate.Core.Repositories;

/// <summary>
/// Repository interface for managing SearchQuery entities in the data store.
/// </summary>
public interface ISearchQueryRepository
{
    /// <summary>
    /// Retrieves a search query by its unique identifier.
    /// </summary>
    /// <param name="id">The search query's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The search query if found; otherwise, null.</returns>
    Task<SearchQuery?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all search queries, ordered by last executed date descending.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all search queries.</returns>
    Task<IReadOnlyList<SearchQuery>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new search query in the data store.
    /// </summary>
    /// <param name="query">The search query to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created search query with generated ID.</returns>
    Task<SearchQuery> CreateAsync(SearchQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing search query in the data store.
    /// </summary>
    /// <param name="query">The search query to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if updated successfully; otherwise, false.</returns>
    Task<bool> UpdateAsync(SearchQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a search query from the data store.
    /// </summary>
    /// <param name="id">The search query's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted successfully; otherwise, false.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
