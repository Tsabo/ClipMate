namespace ClipMate.Networking;

/// <summary>
/// What a Peer answered, and the address it answered at.
/// </summary>
public sealed record PeerAnswer<T>(T Value, string Address);
