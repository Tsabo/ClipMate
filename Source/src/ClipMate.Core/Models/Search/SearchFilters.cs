namespace ClipMate.Core.Models.Search;

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
