using ClipMate.Core.Models;
using ClipMate.Core.ValueObjects;

namespace ClipMate.App.Models.TreeNodes;

/// <summary>
/// Virtual collection that shows search results for a specific query.
/// Each node represents a cached search result with the matching clips.
/// </summary>
public class SearchResultsVirtualCollectionNode : TreeNodeBase
{
    public SearchResultsVirtualCollectionNode(string databaseKey, SearchResult? searchResult = null)
    {
        DatabaseKey = databaseKey ?? throw new ArgumentNullException(nameof(databaseKey));
        SearchResult = searchResult;
    }

    /// <summary>
    /// The database key for loading clips from this virtual collection.
    /// </summary>
    public string DatabaseKey { get; }

    /// <summary>
    /// The underlying collection model representing the search results. Used for metadata only.
    /// </summary>
    public Collection Collection { get; } = new()
    {
        AcceptNewClips = false,
        RetentionLimit = 200,
    };

    /// <summary>
    /// The search result containing the query and matching clip IDs.
    /// Null if no search has been executed yet.
    /// </summary>
    public SearchResult? SearchResult { get; internal set; }

    public override string Name => SearchResult != null
        ? $"Search: \"{SearchResult.Query}\" ({SearchResult.Count} results)"
        : "Search Results";

    public override string Icon => "🔍"; // Search icon

    public override TreeNodeType NodeType => TreeNodeType.VirtualCollection;

    /// <summary>
    /// Virtual ID for search results (not a real collection in the database).
    /// </summary>
    public Guid VirtualId { get; } = Guid.Parse("00000000-0000-0000-0000-000000000002");
}
