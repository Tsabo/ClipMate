namespace ClipMate.Networking.SharedDatabases.Notice;

/// <summary>
/// The Peer shared or unshared a Database, or renamed a Shared Database: ask it for its Shared Databases again.
/// </summary>
public sealed record SharedDatabasesChangedNotice : PeerChangeNotice;