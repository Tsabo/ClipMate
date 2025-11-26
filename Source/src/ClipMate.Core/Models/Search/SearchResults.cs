namespace ClipMate.Core.Models.Search;

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
