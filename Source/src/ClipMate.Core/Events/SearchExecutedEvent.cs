namespace ClipMate.Core.Events;

/// <summary>
/// Messenger event sent when a search is executed.
/// Used to update the SearchResultsCache and create a virtual collection node.
/// </summary>
public class SearchExecutedEvent
{
    /// <summary>
    /// Creates a new search executed event.
    /// </summary>
    /// <param name="databaseKey">The database key where the search was performed.</param>
    /// <param name="query">The search query that was executed.</param>
    /// <param name="clipIds">The list of clip IDs that matched the search.</param>
    public SearchExecutedEvent(string databaseKey, string query, IReadOnlyList<Guid> clipIds)
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
    /// The search query that was executed.
    /// </summary>
    public string Query { get; }

    /// <summary>
    /// The list of clip IDs that matched the search query.
    /// </summary>
    public IReadOnlyList<Guid> ClipIds { get; }
}
