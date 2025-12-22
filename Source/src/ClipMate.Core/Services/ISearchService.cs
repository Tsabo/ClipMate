using ClipMate.Core.Models.Configuration;
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
    /// Builds a SQL query from search filters without executing it.
    /// </summary>
    /// <param name="query">The search query text.</param>
    /// <param name="filters">Optional search filters.</param>
    /// <returns>The generated SQL query string.</returns>
    string BuildSqlQuery(string query, SearchFilters? filters = null);

    /// <summary>
    /// Validates a SQL query for security and syntax.
    /// </summary>
    /// <param name="sql">The SQL query to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple with validation result and optional error message.</returns>
    Task<(bool IsValid, string? ErrorMessage)> ValidateSqlQueryAsync(string sql, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a search query for later reuse in configuration.
    /// </summary>
    /// <param name="name">Display name for the search.</param>
    /// <param name="query">The search query text.</param>
    /// <param name="isCaseSensitive">Whether the search is case-sensitive.</param>
    /// <param name="isRegex">Whether to use regex matching.</param>
    /// <param name="filtersJson">Optional JSON representation of search filters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveSearchQueryAsync(string name, string query, bool isCaseSensitive, bool isRegex, string? filtersJson = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all saved search queries from configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all saved search queries.</returns>
    Task<IReadOnlyList<SavedSearchQuery>> GetSavedQueriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames a saved search query in configuration.
    /// </summary>
    /// <param name="oldName">The current name of the search query.</param>
    /// <param name="newName">The new name for the search query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RenameSearchQueryAsync(string oldName, string newName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a saved search query from configuration.
    /// </summary>
    /// <param name="name">The name of the search query to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteSearchQueryAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the search history (recent searches).
    /// </summary>
    /// <param name="count">Number of recent searches to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of recent search queries.</returns>
    Task<IReadOnlyList<string>> GetSearchHistoryAsync(int count = 10, CancellationToken cancellationToken = default);
}
