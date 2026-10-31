using ClipMate.Core.Models.Networking;

namespace ClipMate.Networking.SharedDatabases.Notice;

/// <summary>
/// A collection or folder of one of the Peer's Shared Databases changed: re-fetch any open view of it.
/// </summary>
/// <param name="Change">Which collection of which Shared Database.</param>
public sealed record CollectionChangedNotice(SharedCollectionChange Change) : PeerChangeNotice;