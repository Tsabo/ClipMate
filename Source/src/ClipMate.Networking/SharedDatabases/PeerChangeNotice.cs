using ClipMate.Core.Models.Networking;

namespace ClipMate.Networking.SharedDatabases;

/// <summary>
/// A change notice from a Peer's <c>WatchChanges</c> stream. Carries IDs only, never clip data.
/// </summary>
public abstract record PeerChangeNotice;

/// <summary>
/// A collection or folder of one of the Peer's Shared Databases changed: re-fetch any open view of it.
/// </summary>
/// <param name="Change">Which collection of which Shared Database.</param>
public sealed record CollectionChangedNotice(SharedCollectionChange Change) : PeerChangeNotice;

/// <summary>
/// The Peer shared or unshared a Database, or renamed a Shared Database: ask it for its Shared Databases again.
/// </summary>
public sealed record SharedDatabasesChangedNotice : PeerChangeNotice;
