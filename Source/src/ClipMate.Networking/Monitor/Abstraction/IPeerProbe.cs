using ClipMate.Core.Models.Networking;

namespace ClipMate.Networking.Monitor.Abstraction;

/// <summary>
/// Connects to a Peer at one address and exchanges Hello.
/// </summary>
public interface IPeerProbe
{
    /// <summary>
    /// Returns what the Peer reported; if it refused this Device, whether that is because this Device is no longer its Peer.
    /// </summary>
    Task<PeerProbeResult> ProbeAsync(DeviceIdentity identity, DeviceId peer, string address, CancellationToken cancellationToken);
}
