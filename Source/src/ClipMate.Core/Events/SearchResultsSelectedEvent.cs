namespace ClipMate.Core.Events;

/// <summary>
/// Messenger event sent when search results are selected in the collection tree.
/// Provides the search query and list of matching clip IDs to display.
/// </summary>
public class SearchResultsSelectedEvent
{
    /// <summary>
    /// Creates a new search results selection event.
    /// </summary>
    /// <param name="databaseKey">The database key where the search was performed.</param>
    /// <param name="query">The search query that produced these results.</param>
    /// <param name="clipIds">The list of clip IDs matching the search query.</param>
    public SearchResultsSelectedEvent(string databaseKey, string query, IReadOnlyList<Guid> clipIds)
    {
        DatabaseKey = databaseKey ?? throw new ArgumentNullException(nameof(databaseKey));
        Query = query ?? throw new ArgumentNullException(nameof(query));
        ClipIds = clipIds ?? throw new ArgumentNullException(nameof(clipIds));
    }

    /// <summary>
    /// The database configuration key where the search was performed.
    /// </summary>
    public string DatabaseKey { get; }

    /// <summary>
    /// The search query that produced these results.
    /// </summary>
    public string Query { get; }

    /// <summary>
    /// The list of clip IDs that match the search query.
    /// </summary>
    public IReadOnlyList<Guid> ClipIds { get; }
}
