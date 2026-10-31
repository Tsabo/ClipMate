using ClipMate.Core.Models.Networking;

namespace ClipMate.Core.Services.Abstractions;

/// <summary>
/// Whether a Peer can be reached right now.
/// </summary>
public enum PeerConnectionStatus
{
    /// <summary>Not checked yet (or networking is off).</summary>
    Unknown,

    /// <summary>The Peer answered and runs a compatible protocol version.</summary>
    Online,

    /// <summary>The Peer could not be reached at any known address.</summary>
    Offline,

    /// <summary>The Peer answered but runs an incompatible protocol major version; one of the Devices needs an update.</summary>
    UpdateRequired,
}

/// <summary>
/// While networking is enabled, regularly connects to each Peer (exchanging protocol version and capabilities)
/// to know which Peers are online, and records when each was last seen.
/// </summary>
public interface IPeerStatusService
{
    /// <summary>
    /// Raised (on a background thread) when a Peer's status changes.
    /// </summary>
    event EventHandler? StatusChanged;

    /// <summary>
    /// Gets the current status of the Peer with <paramref name="deviceId" />.
    /// </summary>
    PeerConnectionStatus GetStatus(DeviceId deviceId);

    /// <summary>
    /// Starts checking the Peers as <paramref name="identity" />. Returns without waiting for the first check.
    /// </summary>
    Task StartAsync(DeviceIdentity identity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops checking; every status goes back to <see cref="PeerConnectionStatus.Unknown" />.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks every Peer now. Does nothing while stopped.
    /// </summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);
}
