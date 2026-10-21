using ClipMate.Core.Models.Networking;

namespace ClipMate.Core.Services;

/// <summary>
/// The persisted Peer records (in clipmate.toml): the single source of truth for which Devices are Peers.
/// Only a Device recorded here is trusted; pending or declined pairing requests are never recorded.
/// Safe to call from any thread.
/// </summary>
public interface IPeerStore
{
    /// <summary>
    /// Raised (on the calling thread) after a Peer record is added or changed.
    /// </summary>
    event EventHandler? PeersChanged;

    /// <summary>
    /// Gets all Peers.
    /// </summary>
    IReadOnlyList<Peer> GetPeers();

    /// <summary>
    /// Gets the Peer with <paramref name="deviceId" />, or <c>null</c> if that Device is not paired.
    /// </summary>
    Peer? Find(DeviceId deviceId);

    /// <summary>
    /// Returns <c>true</c> if the Device with <paramref name="deviceId" /> is a Peer.
    /// </summary>
    bool IsPaired(DeviceId deviceId);

    /// <summary>
    /// Records a completed Pairing (or a repeated one), replacing the Peer Alias. Call only once both Devices have accepted.
    /// </summary>
    /// <param name="deviceId">The Peer's Device ID.</param>
    /// <param name="deviceName">The Peer's Device Name.</param>
    /// <param name="alias">The Peer Alias from the "Show as" field; blank means no alias.</param>
    /// <param name="address">The address the Peer was reached at (<c>host:port</c>), if known.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SavePairedAsync(DeviceId deviceId, string deviceName, string? alias, string? address, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a Peer was reached: updates its Device Name, last seen time and last known addresses.
    /// Does nothing if the Device is not a Peer.
    /// </summary>
    Task RecordSeenAsync(DeviceId deviceId, string deviceName, string? address, DateTime seenAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the Shared Databases a Peer reported. Does nothing if the Device is not a Peer. Does not raise
    /// <see cref="PeersChanged" />: who the Peers are has not changed.
    /// </summary>
    Task RecordSharedDatabasesAsync(DeviceId deviceId, IReadOnlyList<SharedDatabase> sharedDatabases, CancellationToken cancellationToken = default);
}
