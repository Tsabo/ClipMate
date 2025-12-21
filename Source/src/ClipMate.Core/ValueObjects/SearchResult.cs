namespace ClipMate.Core.ValueObjects;

/// <summary>
/// Value object representing search results for a specific query.
/// Immutable - once created, the query and clip IDs cannot be changed.
/// </summary>
public sealed record SearchResult
{
    /// <summary>
    /// Creates a new search result.
    /// </summary>
    /// <param name="query">The search query that produced these results.</param>
    /// <param name="clipIds">The clip IDs matching the query.</param>
    public SearchResult(string query, IReadOnlyList<Guid> clipIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentNullException.ThrowIfNull(clipIds);

        Query = query;
        ClipIds = clipIds;
        SearchedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// The search query string.
    /// </summary>
    public string Query { get; }

    /// <summary>
    /// The clip IDs that matched the search query.
    /// </summary>
    public IReadOnlyList<Guid> ClipIds { get; }

    /// <summary>
    /// When this search was performed.
    /// </summary>
    public DateTimeOffset SearchedAt { get; }

    /// <summary>
    /// Number of clips found in this search.
    /// </summary>
    public int Count => ClipIds.Count;
}
