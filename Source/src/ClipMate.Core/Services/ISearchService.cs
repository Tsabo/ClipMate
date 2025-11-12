using ClipMate.Core.Models;

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

/// <summary>
/// Filters for advanced search operations.
/// </summary>
public class SearchFilters
{
    /// <summary>
    /// Content types to include in search (null = all types).
    /// </summary>
    public IEnumerable<ClipType>? ContentTypes { get; init; }

    /// <summary>
    /// Filter by date range (clips captured within this range).
    /// </summary>
    public DateRange? DateRange { get; init; }

    /// <summary>
    /// Search scope (current collection, all collections, etc.).
    /// </summary>
    public SearchScope Scope { get; init; } = SearchScope.AllCollections;

    /// <summary>
    /// Collection ID to search within (when Scope = CurrentCollection).
    /// </summary>
    public Guid? CollectionId { get; init; }

    /// <summary>
    /// Folder ID to search within (when Scope = CurrentFolder).
    /// </summary>
    public Guid? FolderId { get; init; }

    /// <summary>
    /// Whether to use case-sensitive search.
    /// </summary>
    public bool CaseSensitive { get; init; }

    /// <summary>
    /// Whether to use regex pattern matching.
    /// </summary>
    public bool IsRegex { get; init; }
}

/// <summary>
/// Date range for search filtering.
/// </summary>
public record DateRange(DateTime? From, DateTime? To);

/// <summary>
/// Search scope enumeration.
/// </summary>
public enum SearchScope
{
    /// <summary>
    /// Search in all collections.
    /// </summary>
    AllCollections,

    /// <summary>
    /// Search in the current/active collection only.
    /// </summary>
    CurrentCollection,

    /// <summary>
    /// Search in a specific folder.
    /// </summary>
    CurrentFolder
}

/// <summary>
/// Results from a search operation.
/// </summary>
public class SearchResults
{
    /// <summary>
    /// The matching clips.
    /// </summary>
    public required IReadOnlyList<Clip> Clips { get; init; }

    /// <summary>
    /// Total number of matches found.
    /// </summary>
    public int TotalMatches { get; init; }

    /// <summary>
    /// The search query that was executed.
    /// </summary>
    public required string Query { get; init; }
}
