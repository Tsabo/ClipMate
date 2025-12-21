using ClipMate.Core.ValueObjects;

namespace ClipMate.App.ViewModels;

/// <summary>
/// Virtual collection that shows search results for a specific query.
/// Each node represents a cached search result with the matching clips.
/// </summary>
public class SearchResultsVirtualCollectionNode : TreeNodeBase
{
    public SearchResultsVirtualCollectionNode(string databaseKey, SearchResult searchResult)
    {
        DatabaseKey = databaseKey ?? throw new ArgumentNullException(nameof(databaseKey));
        SearchResult = searchResult ?? throw new ArgumentNullException(nameof(searchResult));
    }

    /// <summary>
    /// The database key for loading clips from this virtual collection.
    /// </summary>
    public string DatabaseKey { get; }

    /// <summary>
    /// The search result containing the query and matching clip IDs.
    /// </summary>
    public SearchResult SearchResult { get; }

    public override string Name => $"Search: \"{SearchResult.Query}\" ({SearchResult.Count} results)";

    public override string Icon => "🔍"; // Search icon

    public override TreeNodeType NodeType => TreeNodeType.VirtualCollection;

    /// <summary>
    /// Virtual ID for search results (not a real collection in the database).
    /// </summary>
    public Guid VirtualId { get; } = Guid.Parse("00000000-0000-0000-0000-000000000002");
}
