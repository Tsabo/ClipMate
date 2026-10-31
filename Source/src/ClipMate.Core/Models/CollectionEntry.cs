namespace ClipMate.Core.Models;

/// <summary>
/// What kind of entry a <see cref="CollectionEntry" /> is.
/// </summary>
public enum CollectionEntryKind
{
    /// <summary>A collection that stores clips.</summary>
    Collection,

    /// <summary>A folder inside a collection (or inside another folder).</summary>
    Folder,

    /// <summary>A Smart (virtual) collection; its contents are evaluated where the Database is.</summary>
    Smart,

    /// <summary>A SQL-based collection; its contents are evaluated where the Database is, with its own stored SQL.</summary>
    SqlBased,
}

/// <summary>
/// One entry of a Database's collection and folder tree, local or remote: the flat list a tree is built from. Carries
/// no SQL: Smart and SQL-based collections are only listed, and their contents are always evaluated where the Database
/// is (on the Peer, for a Remote Database).
/// </summary>
/// <param name="Id">The collection or folder ID within its Database.</param>
/// <param name="ParentId">The collection or folder it belongs to, or <c>null</c> for a top-level entry.</param>
/// <param name="Title">The collection or folder name.</param>
/// <param name="Kind">What kind of entry this is.</param>
/// <param name="SortKey">The manual sort order.</param>
public sealed record CollectionEntry(Guid Id, Guid? ParentId, string Title, CollectionEntryKind Kind, int SortKey)
{
    /// <summary>
    /// Describes <paramref name="collection" /> for a Peer, leaving out its SQL and everything else a Peer is not served.
    /// </summary>
    public static CollectionEntry From(Collection collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        var kind = collection switch
        {
            { ListType: CollectionListType.SqlBased } => CollectionEntryKind.SqlBased,
            { IsVirtual: true } => CollectionEntryKind.Smart,

            // Top-level rows with LmType Folder (e.g. "Safe", "Overflow") are shown as collections.
            { LmType: CollectionLmType.Folder, ParentId: not null } => CollectionEntryKind.Folder,
            var _ => CollectionEntryKind.Collection,
        };

        return new CollectionEntry(collection.Id, collection.ParentId, collection.Title, kind, collection.SortKey);
    }
}
