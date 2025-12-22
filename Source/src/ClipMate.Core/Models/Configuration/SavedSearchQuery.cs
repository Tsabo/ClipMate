namespace ClipMate.Core.Models.Configuration;

/// <summary>
/// Represents a saved search query stored in configuration.
/// Unlike database-stored SearchQuery, this is configuration-based and shareable across databases.
/// </summary>
public class SavedSearchQuery
{
    /// <summary>
    /// Display name for the saved search.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The search query text or description.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Whether the search is case-sensitive.
    /// </summary>
    public bool IsCaseSensitive { get; set; }

    /// <summary>
    /// Whether to use regular expression matching.
    /// </summary>
    public bool IsRegex { get; set; }

    /// <summary>
    /// Search filters in JSON format for persistence.
    /// </summary>
    public string? FiltersJson { get; set; }
}
