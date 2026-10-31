using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Networking.Monitor;
using ClipMate.Networking.Protocol;
using Microsoft.Extensions.Logging;

namespace ClipMate.Networking;

/// <summary>
/// Unpairs a Peer here at once, then tells it over a Peer connection so it unpairs too. A Peer that cannot be told is
/// refused from then on, and <see cref="PeerStatusMonitor" /> on that Device finds out it was unpaired
/// (through <c>CheckPairing</c>) the next time it reaches this Device.
/// </summary>
public sealed class PeerUnpairingService : IUnpairingService
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);
    private readonly IDeviceDiscoveryService _discovery;
    private readonly ILocalDevice _localDevice;
    private readonly ILogger<PeerUnpairingService> _logger;
    private readonly IPeerStore _peerStore;

    public PeerUnpairingService(ILocalDevice localDevice, IPeerStore peerStore, IDeviceDiscoveryService discovery,
        ILogger<PeerUnpairingService> logger)
    {
        _localDevice = localDevice ?? throw new ArgumentNullException(nameof(localDevice));
        _peerStore = peerStore ?? throw new ArgumentNullException(nameof(peerStore));
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<bool> UnpairAsync(DeviceId deviceId, CancellationToken cancellationToken = default)
    {
        var peer = _peerStore.Find(deviceId);

        // Unpair here first: from now on the Device is refused, and its open connections are dropped.
        await _peerStore.RemoveAsync(deviceId, cancellationToken);

        var identity = _localDevice.Identity;
        if (peer == null || identity == null)
            return false;

        foreach (var address in PeerAddress.GetAddressesToTry(peer, _discovery.DiscoveredDevices))
        {
            if (await TryTellAsync(identity, deviceId, address, cancellationToken))
                return true;
        }

        _logger.LogInformation("Could not tell Device {DeviceId} it was unpaired; it will find out when it next reaches this Device",
            deviceId.ToDisplayString());

        return false;
    }

    private async Task<bool> TryTellAsync(DeviceIdentity identity, DeviceId deviceId, string address, CancellationToken cancellationToken)
    {
        try
        {
            using var channel = PeerChannelFactory.CreateForPeer(address, identity, deviceId);
            var client = new PeerService.PeerServiceClient(channel);
            await client.UnpairAsync(new UnpairRequest(), deadline: DateTime.UtcNow + _timeout, cancellationToken: cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(ex, "Could not tell Device {DeviceId} at {Address} it was unpaired", deviceId.ToShortDisplayString(), address);
            return false;
        }
    }
}
