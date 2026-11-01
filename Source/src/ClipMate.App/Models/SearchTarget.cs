using ClipMate.Core.Models;
using ClipMate.Core.Models.Networking;

namespace ClipMate.App.Models;

/// <summary>
/// What the search dialog searches: a local Database or a Remote Database, and the collection and folder selected in it
/// (which the "current collection" and "specific folder" scopes refer to).
/// </summary>
/// <param name="DatabaseKey">The Database's key; a <see cref="RemoteDatabaseKey" /> for a Remote Database.</param>
/// <param name="DatabaseName">The Database's name as the collection tree shows it.</param>
public sealed record SearchTarget(string DatabaseKey, string DatabaseName)
{
    /// <summary>
    /// The selected collection, or <c>null</c> when none is selected or it cannot be searched by membership (a Smart or
    /// SQL-based collection, the Trashcan, search results).
    /// </summary>
    public Guid? CollectionId { get; init; }

    /// <summary>
    /// The selected collection's name.
    /// </summary>
    public string? CollectionName { get; init; }

    /// <summary>
    /// The selected folder, or <c>null</c> when no folder is selected.
    /// </summary>
    public Guid? FolderId { get; init; }

    /// <summary>
    /// The selected folder's name.
    /// </summary>
    public string? FolderName { get; init; }

    /// <summary>
    /// Gets whether the target is a Remote Database: searched by its Peer, with no SQL. Set by whoever makes the
    /// target from a Remote Database's node.
    /// </summary>
    public bool IsRemote { get; init; }
}

/// <summary>
/// A search scope the search dialog offers, with its label.
/// </summary>
/// <param name="Scope">The scope.</param>
/// <param name="Label">How the dialog shows it, e.g. "Collection: Inbox".</param>
public sealed record SearchScopeOption(SearchScope Scope, string Label);
