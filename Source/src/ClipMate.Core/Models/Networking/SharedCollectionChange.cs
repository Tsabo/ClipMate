namespace ClipMate.Core.Models.Networking;

/// <summary>
/// A "collection changed" notice for a Shared Database: which collection (or folder) changed, never what changed in it.
/// Peers re-fetch any open view of that collection when they receive it.
/// </summary>
/// <param name="DatabaseId">The Database ID of the Shared Database.</param>
/// <param name="CollectionId">The collection or folder whose clips or properties changed.</param>
public sealed record SharedCollectionChange(Guid DatabaseId, Guid CollectionId);

/// <summary>
/// Event data for <see cref="SharedCollectionChange" />.
/// </summary>
public sealed class SharedCollectionChangedEventArgs(SharedCollectionChange change) : EventArgs
{
    /// <summary>
    /// Gets the change.
    /// </summary>
    public SharedCollectionChange Change { get; } = change ?? throw new ArgumentNullException(nameof(change));
}

/// <summary>
/// Event data for a change in a collection of a Remote Database, reported by its Peer.
/// </summary>
public sealed class RemoteCollectionChangedEventArgs(RemoteDatabaseAddress address, Guid collectionId) : EventArgs
{
    /// <summary>
    /// Gets the Remote Database.
    /// </summary>
    public RemoteDatabaseAddress Address { get; } = address;

    /// <summary>
    /// Gets the collection or folder that changed.
    /// </summary>
    public Guid CollectionId { get; } = collectionId;
}
