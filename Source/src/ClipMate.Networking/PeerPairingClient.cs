using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Networking.Internal;
using Microsoft.Extensions.Logging;

namespace ClipMate.Networking;

/// <summary>
/// Sends pairing requests to other Devices over a pairing connection (<see cref="PeerChannelFactory.CreateForPairing" />).
/// </summary>
public sealed class PeerPairingClient : IPairingClient
{
    private readonly ILocalDevice _localDevice;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IPeerStore _peerStore;

    public PeerPairingClient(ILocalDevice localDevice, IPeerStore peerStore, ILoggerFactory loggerFactory)
    {
        _localDevice = localDevice ?? throw new ArgumentNullException(nameof(localDevice));
        _peerStore = peerStore ?? throw new ArgumentNullException(nameof(peerStore));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    /// <inheritdoc />
    public IPairingSession Start(PairingTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var identity = _localDevice.Identity ?? throw new InvalidOperationException("Turn on networking before pairing.");
        return new PairingSession(identity, _localDevice.DeviceName, _localDevice.ListeningPort ?? 0, target, _peerStore,
            _loggerFactory.CreateLogger<PairingSession>());
    }
}
