namespace ClipMate.Networking.Monitor;

/// <summary>
/// The outcome of probing a Peer at one address.
/// </summary>
/// <param name="Reply">What the Peer reported in Hello, or <c>null</c> if it did not answer.</param>
/// <param name="Unpaired">
/// <c>true</c> if the Device answered that this Device is not its Peer (it unpaired this Device, or its Pairing never
/// completed there).
/// </param>
public sealed record PeerProbeResult(PeerProbeReply? Reply, bool Unpaired)
{
    /// <summary>
    /// The Peer could not be reached there (or is not that Device).
    /// </summary>
    public static PeerProbeResult Unreachable { get; } = new(null, false);

    /// <summary>
    /// The Device is no longer paired with this Device.
    /// </summary>
    public static PeerProbeResult NotPaired { get; } = new(null, true);

    /// <summary>
    /// The Peer answered Hello with <paramref name="reply" />.
    /// </summary>
    public static PeerProbeResult Reached(PeerProbeReply reply) => new(reply, false);
}
