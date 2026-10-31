using ClipMate.Core.Models.Networking;

namespace ClipMate.Core.Services.Abstractions;

/// <summary>
/// The serving side of live change notifications: reports which collections of this Device's Shared Databases changed
/// (a clip captured, deleted, moved or edited, or a collection changed), and when the list of Shared Databases changed.
/// Changes in Databases that are not shared are never reported. Only IDs are reported, never clip data.
/// <para>
/// Changes are watched only while something is subscribed, so a Device that serves no Peer pays nothing for it.
/// Events are raised on a background thread.
/// </para>
/// </summary>
public interface ISharedDatabaseChangeFeed
{
    /// <summary>
    /// Raised when a collection or folder of a Shared Database changed.
    /// </summary>
    event EventHandler<SharedCollectionChangedEventArgs>? CollectionChanged;

    /// <summary>
    /// Raised when a Database is shared or unshared, or a Shared Database is renamed, added or removed.
    /// </summary>
    event EventHandler? SharedDatabasesChanged;
}
