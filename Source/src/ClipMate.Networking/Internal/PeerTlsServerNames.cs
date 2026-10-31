namespace ClipMate.Networking.Internal;

/// <summary>
/// TLS server names (SNI) that tell the Peer listener what a connection is for.
/// </summary>
internal static class PeerTlsServerNames
{
    /// <summary>
    /// A connection between Peers. Only paired Devices complete the TLS handshake.
    /// </summary>
    public const string Peer = "peer.clipmate.invalid";

    /// <summary>
    /// A connection for Pairing. Any Device with a certificate completes the TLS handshake, but only the pairing calls are
    /// served to it.
    /// </summary>
    public const string Pairing = "pairing.clipmate.invalid";
}
