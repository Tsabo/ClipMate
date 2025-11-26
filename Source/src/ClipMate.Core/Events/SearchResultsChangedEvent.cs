using ClipMate.Core.Models;

namespace ClipMate.Core.Events;

/// <summary>
/// Messenger event sent when search results change.
/// Allows ClipList to display filtered results without direct coupling to SearchViewModel.
/// </summary>
public class SearchResultsChangedEvent
{
    /// <summary>
    /// Creates a new search results changed event.
    /// </summary>
    /// <param name="searchText">The search text.</param>
    /// <param name="results">The search results.</param>
    public SearchResultsChangedEvent(string? searchText, IReadOnlyList<Clip> results)
    {
        SearchText = searchText;
        Results = results ?? [];
    }

    /// <summary>
    /// The search text that was used, or null/empty if search was cleared.
    /// </summary>
    public string? SearchText { get; }

    /// <summary>
    /// The search results, or empty if search was cleared or no results found.
    /// </summary>
    public IReadOnlyList<Clip> Results { get; }
}
