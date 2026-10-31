using System.Net.Sockets;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Networking.Monitor.Abstraction;
using ClipMate.Networking.Protocol;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace ClipMate.Networking.Monitor;

/// <summary>
/// <see cref="IPeerProbe" /> over a Peer connection (<see cref="PeerChannelFactory.CreateForPeer" />). If the Device is
/// reached but refuses the Peer connection, asks it over a pairing connection whether this Device is still its Peer.
/// </summary>
public sealed class GrpcPeerProbe : IPeerProbe
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<GrpcPeerProbe> _logger;

    public GrpcPeerProbe(IConfigurationService configurationService, ILogger<GrpcPeerProbe> logger)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<PeerProbeResult> ProbeAsync(DeviceIdentity identity, DeviceId peer, string address, CancellationToken cancellationToken)
    {
        try
        {
            using var channel = PeerChannelFactory.CreateForPeer(address, identity, peer);
            var client = new PeerService.PeerServiceClient(channel);
            var request = new HelloRequest
            {
                ProtocolMajor = PeerProtocol.Major,
                ProtocolMinor = PeerProtocol.Minor,
                DeviceName = _configurationService.Configuration.Networking.DeviceName,
            };

            request.Capabilities.AddRange(PeerProtocol.Capabilities);

            var reply = await client.HelloAsync(request, deadline: DateTime.UtcNow + _timeout, cancellationToken: cancellationToken);
            return PeerProbeResult.Reached(new PeerProbeReply(reply.DeviceName, reply.ProtocolMajor, reply.ProtocolMinor, reply.Capabilities.ToList()));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(ex, "Peer {DeviceId} not reachable at {Address}", peer.ToShortDisplayString(), address);
            if (IsConnectFailure(ex))
                return PeerProbeResult.Unreachable;
        }

        return await CheckPairingAsync(identity, peer, address, cancellationToken);
    }

    private async Task<PeerProbeResult> CheckPairingAsync(DeviceIdentity identity, DeviceId peer, string address, CancellationToken cancellationToken)
    {
        try
        {
            // A pairing connection checks that the other end is this Peer, so only the Peer itself can say it unpaired.
            using var channel = PeerChannelFactory.CreateForPairing(address, identity, peer);
            var client = new PeerService.PeerServiceClient(channel);
            var reply = await client.CheckPairingAsync(new CheckPairingRequest(), deadline: DateTime.UtcNow + _timeout,
                cancellationToken: cancellationToken);

            return reply.Paired
                ? PeerProbeResult.Unreachable
                : PeerProbeResult.NotPaired;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(ex, "Could not check Pairing with Peer {DeviceId} at {Address}", peer.ToShortDisplayString(), address);
            return PeerProbeResult.Unreachable;
        }
    }

    // Nothing answered at the address (as opposed to a Device that answered but refused the connection).
    private static bool IsConnectFailure(Exception exception)
    {
        if (exception is RpcException { StatusCode: StatusCode.DeadlineExceeded })
            return true;

        for (var item = exception; item != null; item = item.InnerException)
        {
            if (item is SocketException or TimeoutException)
                return true;
        }

        return exception is RpcException { Status.DebugException: { } debug } && IsConnectFailure(debug);
    }
}
