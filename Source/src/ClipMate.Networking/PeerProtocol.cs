namespace ClipMate.Networking;

/// <summary>
/// Version of the Peer wire protocol defined in <c>Protos/peer.proto</c>.
/// </summary>
public static class PeerProtocol
{
    /// <summary>
    /// Incompatible changes bump the major version; Peers with a different major version show "update required".
    /// Version 2 is the baseline every Peer supports: pairing, Shared Databases, the clips of Shared Databases, change
    /// notices and Remote Search.
    /// </summary>
    public const uint Major = 2;

    /// <summary>
    /// Backward-compatible additions bump the minor version.
    /// </summary>
    public const uint Minor = 0;

    /// <summary>
    /// The optional features this Device supports, exchanged in Hello. A feature added after version 2.0 gets a
    /// capability here, and a Device checks that its Peer announced it before using the feature.
    /// </summary>
    public static IReadOnlyList<string> Capabilities { get; } = [];
}
