using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace ClipMate.Networking;

/// <summary>
/// The one way this Device reaches its Peers: as its own identity, over mutual TLS, at the address a Peer announces on
/// the LAN and then at its last known addresses.
/// </summary>
public sealed class PeerConnector
{
    private readonly IDeviceDiscoveryService _discovery;
    private readonly ILocalDevice _localDevice;
    private readonly ILogger<PeerConnector> _logger;
    private readonly IPeerStore _peerStore;

    public PeerConnector(ILocalDevice localDevice, IPeerStore peerStore, IDeviceDiscoveryService discovery, ILogger<PeerConnector> logger)
    {
        _localDevice = localDevice ?? throw new ArgumentNullException(nameof(localDevice));
        _peerStore = peerStore ?? throw new ArgumentNullException(nameof(peerStore));
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets how to reach the Peer with <paramref name="deviceId" /> now, or <c>null</c> if networking is off or that
    /// Device is not a Peer.
    /// </summary>
    public PeerConnection? ForPeer(DeviceId deviceId)
    {
        var identity = _localDevice.Identity;
        if (identity == null)
            return null;

        var peer = _peerStore.Find(deviceId);
        return peer == null
            ? null
            : new PeerConnection(identity, peer, PeerAddress.GetAddressesToTry(peer, _discovery.DiscoveredDevices).ToList(), _logger);
    }
}
