namespace ClipMate.Core.Models.Networking;

/// <summary>
/// What kind of entry a <see cref="SharedCollection" /> is.
/// </summary>
public enum SharedCollectionKind
{
    /// <summary>A collection that stores clips.</summary>
    Collection,

    /// <summary>A folder inside a collection (or inside another folder).</summary>
    Folder,

    /// <summary>A Smart (virtual) collection; its contents are evaluated on the Peer.</summary>
    Smart,

    /// <summary>A SQL-based collection; its contents are evaluated on the Peer with the Peer's own stored SQL.</summary>
    SqlBased,
}

/// <summary>
/// One entry of a Shared Database's collection and folder tree, as a Peer serves it. Carries no SQL: Smart and
/// SQL-based collections are only listed, and their contents are always evaluated on the Peer.
/// </summary>
/// <param name="Id">The collection or folder ID within its Database.</param>
/// <param name="ParentId">The collection or folder it belongs to, or <c>null</c> for a top-level entry.</param>
/// <param name="Title">The collection or folder name.</param>
/// <param name="Kind">What kind of entry this is.</param>
/// <param name="SortKey">The manual sort order on the Peer.</param>
public sealed record SharedCollection(Guid Id, Guid? ParentId, string Title, SharedCollectionKind Kind, int SortKey)
{
    /// <summary>
    /// Describes <paramref name="collection" /> for a Peer, leaving out its SQL and everything else a Peer is not served.
    /// </summary>
    public static SharedCollection From(Collection collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        var kind = collection switch
        {
            { ListType: CollectionListType.SqlBased } => SharedCollectionKind.SqlBased,
            { IsVirtual: true } => SharedCollectionKind.Smart,

            // Top-level rows with LmType Folder (e.g. "Safe", "Overflow") are shown as collections.
            { LmType: CollectionLmType.Folder, ParentId: not null } => SharedCollectionKind.Folder,
            var _ => SharedCollectionKind.Collection,
        };

        return new SharedCollection(collection.Id, collection.ParentId, collection.Title, kind, collection.SortKey);
    }
}
