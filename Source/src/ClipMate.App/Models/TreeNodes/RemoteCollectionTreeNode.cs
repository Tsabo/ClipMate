using ClipMate.Core.Models.Networking;

namespace ClipMate.App.Models.TreeNodes;

/// <summary>
/// A collection, Smart collection or SQL-based collection in a Remote Database. Read-only: deliberately not a
/// <see cref="CollectionTreeNode" />, so no local collection action (move, delete, properties, drop target) applies to it.
/// </summary>
public sealed class RemoteCollectionTreeNode : TreeNodeBase
{
    public RemoteCollectionTreeNode(SharedCollection collection)
    {
        Collection = collection ?? throw new ArgumentNullException(nameof(collection));
        SortKey = collection.SortKey;
    }

    /// <summary>
    /// The collection as the Peer serves it.
    /// </summary>
    public SharedCollection Collection { get; }

    /// <inheritdoc />
    public override string Name => Collection.Title;

    /// <inheritdoc />
    public override string Icon => Collection.Kind switch
    {
        SharedCollectionKind.Smart or SharedCollectionKind.SqlBased => "🔍",
        var _ => Collection.Title.ToLowerInvariant() switch
        {
            "inbox" => "📥",
            "safe" => "🔒",
            "overflow" => "🌊",
            "samples" => "📋",
            var _ => "📁",
        },
    };

    /// <inheritdoc />
    public override TreeNodeType NodeType => Collection.Kind is SharedCollectionKind.Smart or SharedCollectionKind.SqlBased
        ? TreeNodeType.VirtualCollection
        : TreeNodeType.Collection;
}

/// <summary>
/// A folder in a Remote Database. Read-only, like <see cref="RemoteCollectionTreeNode" />.
/// </summary>
public sealed class RemoteFolderTreeNode : TreeNodeBase
{
    public RemoteFolderTreeNode(SharedCollection folder)
    {
        Folder = folder ?? throw new ArgumentNullException(nameof(folder));
        SortKey = folder.SortKey;
    }

    /// <summary>
    /// The folder as the Peer serves it.
    /// </summary>
    public SharedCollection Folder { get; }

    /// <inheritdoc />
    public override string Name => Folder.Title;

    /// <inheritdoc />
    public override string Icon => "📁";

    /// <inheritdoc />
    public override TreeNodeType NodeType => TreeNodeType.Folder;
}
