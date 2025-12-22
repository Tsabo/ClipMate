namespace ClipMate.Core.Models.Search;

/// <summary>
/// Filters for advanced search operations.
/// </summary>
public class SearchFilters
{
    /// <summary>
    /// Search in clip titles.
    /// </summary>
    public string? TitleQuery { get; init; }

    /// <summary>
    /// Search in clip text content.
    /// </summary>
    public string? TextContentQuery { get; init; }

    /// <summary>
    /// Search in creator field.
    /// </summary>
    public string? CreatorQuery { get; init; }

    /// <summary>
    /// Search in source URL field.
    /// </summary>
    public string? SourceUrlQuery { get; init; }

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
    /// Filter by specific format (clipboard format).
    /// </summary>
    public string? Format { get; init; }

    /// <summary>
    /// Filter to only encrypted clips.
    /// </summary>
    public bool? EncryptedOnly { get; init; }

    /// <summary>
    /// Filter to only clips with shortcuts.
    /// </summary>
    public bool? HasShortcutOnly { get; init; }

    /// <summary>
    /// Include deleted clips in results.
    /// </summary>
    public bool IncludeDeleted { get; init; }

    /// <summary>
    /// Whether to use case-sensitive search.
    /// </summary>
    public bool CaseSensitive { get; init; }

    /// <summary>
    /// Whether to use regex pattern matching.
    /// </summary>
    public bool IsRegex { get; init; }
}
