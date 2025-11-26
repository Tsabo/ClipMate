using ClipMate.Core.Models;
using ClipMate.Core.Models.Search;

namespace ClipMate.Core.Services;

/// <summary>
/// Service for searching clips across the history with advanced filtering.
/// </summary>
public interface ISearchService
{
    /// <summary>
    /// Searches clips by text content with optional filters.
    /// </summary>
    /// <param name="query">The search query text.</param>
    /// <param name="filters">Optional search filters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Search results with matching clips.</returns>
    Task<SearchResults> SearchAsync(string query, SearchFilters? filters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches clips using a saved search query.
    /// </summary>
    /// <param name="searchQueryId">The ID of the saved search query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Search results with matching clips.</returns>
    Task<SearchResults> ExecuteSavedSearchAsync(Guid searchQueryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a search query for later reuse.
    /// </summary>
    /// <param name="name">Display name for the search.</param>
    /// <param name="query">The search query text.</param>
    /// <param name="isCaseSensitive">Whether the search is case-sensitive.</param>
    /// <param name="isRegex">Whether to use regex matching.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created SearchQuery entity.</returns>
    Task<SearchQuery> SaveSearchQueryAsync(string name, string query, bool isCaseSensitive, bool isRegex, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a saved search query.
    /// </summary>
    /// <param name="id">The search query ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteSearchQueryAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the search history (recent searches).
    /// </summary>
    /// <param name="count">Number of recent searches to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of recent search queries.</returns>
    Task<IReadOnlyList<string>> GetSearchHistoryAsync(int count = 10, CancellationToken cancellationToken = default);
}
