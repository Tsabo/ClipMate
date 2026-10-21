namespace ClipMate.Networking;

/// <summary>
/// Version of the Peer wire protocol defined in <c>Protos/peer.proto</c>.
/// </summary>
public static class PeerProtocol
{
    /// <summary>
    /// Incompatible changes bump the major version; Peers with a different major version show "update required".
    /// </summary>
    public const uint Major = 1;

    /// <summary>
    /// Backward-compatible additions bump the minor version.
    /// </summary>
    public const uint Minor = 0;
}
