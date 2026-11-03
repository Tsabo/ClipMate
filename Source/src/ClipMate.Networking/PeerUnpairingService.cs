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
    private readonly PeerConnector _connector;
    private readonly ILogger<PeerUnpairingService> _logger;
    private readonly IPeerStore _peerStore;

    public PeerUnpairingService(PeerConnector connector, IPeerStore peerStore, ILogger<PeerUnpairingService> logger)
    {
        _connector = connector ?? throw new ArgumentNullException(nameof(connector));
        _peerStore = peerStore ?? throw new ArgumentNullException(nameof(peerStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<bool> UnpairAsync(DeviceId deviceId, CancellationToken cancellationToken = default)
    {
        // How to reach it, taken while it is still a Peer.
        var peer = _connector.ForPeer(deviceId);

        // Unpair here first: from now on the Device is refused, and its open connections are dropped.
        await _peerStore.RemoveAsync(deviceId, cancellationToken);

        if (peer == null)
            return false;

        try
        {
            var reply = await peer.CallAsync((channel, token) =>
                new PeerService.PeerServiceClient(channel).UnpairAsync(new UnpairRequest(), deadline: DateTime.UtcNow + _timeout,
                    cancellationToken: token).ResponseAsync, cancellationToken);

            if (reply != null)
                return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(ex, "Device {DeviceId} did not accept being unpaired", deviceId.ToShortDisplayString());
        }

        _logger.LogInformation("Could not tell Device {DeviceId} it was unpaired; it will find out when it next reaches this Device",
            deviceId.ToDisplayString());

        return false;
    }
}
