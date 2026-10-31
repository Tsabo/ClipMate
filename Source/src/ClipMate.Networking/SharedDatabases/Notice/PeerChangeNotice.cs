namespace ClipMate.Networking.SharedDatabases.Notice;

/// <summary>
/// A change notice from a Peer's <c>WatchChanges</c> stream. Carries IDs only, never clip data.
/// </summary>
public abstract record PeerChangeNotice;
