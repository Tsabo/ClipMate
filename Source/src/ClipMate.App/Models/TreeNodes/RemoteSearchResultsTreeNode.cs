using ClipMate.Core.Models.Networking;
using ClipMate.Core.ValueObjects;

namespace ClipMate.App.Models.TreeNodes;

/// <summary>
/// The Search Results node of a Remote Database: shows the latest Remote Search results, which are kept on this Device
/// only. Read-only, and deliberately not a <see cref="SearchResultsVirtualCollectionNode" />, whose clips load from a
/// local database.
/// </summary>
public sealed class RemoteSearchResultsTreeNode : TreeNodeBase
{
    public RemoteSearchResultsTreeNode(RemoteDatabaseAddress address, RemoteSearchResult? result = null)
    {
        Address = address;
        Result = result;
    }

    /// <summary>
    /// The Remote Database the results came from.
    /// </summary>
    public RemoteDatabaseAddress Address { get; }

    /// <summary>
    /// The latest Remote Search results, or <c>null</c> before the first search.
    /// </summary>
    public RemoteSearchResult? Result
    {
        get;
        set
        {
            if (!SetProperty(ref field, value))
                return;

            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(DisplayText));
        }
    }

    /// <inheritdoc />
    public override string Name => Result == null ? "Search Results" : $"Search: {Result.Describe()}";

    /// <inheritdoc />
    public override string Icon => "🔍";

    /// <inheritdoc />
    public override TreeNodeType NodeType => TreeNodeType.VirtualCollection;
}
