namespace ClipMate.Core.Models;

/// <summary>
/// Represents a saved search query for quick re-execution.
/// </summary>
public class SearchQuery
{
    /// <summary>
    /// Unique identifier for the search query.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Display name for the saved search.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The search query text.
    /// </summary>
    public string QueryText { get; set; } = string.Empty;

    /// <summary>
    /// Whether the search is case-sensitive.
    /// </summary>
    public bool IsCaseSensitive { get; set; }

    /// <summary>
    /// Whether to use regular expression matching.
    /// </summary>
    public bool IsRegex { get; set; }

    /// <summary>
    /// Number of times this search has been executed.
    /// </summary>
    public int ExecutionCount { get; set; }

    /// <summary>
    /// Timestamp when the search was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp of last execution.
    /// </summary>
    public DateTime? LastExecutedAt { get; set; }
}
