using System.Net;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Networking.Protocol;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace ClipMate.Networking.Internal;

/// <summary>
/// Serves the Peer protocol. <see cref="PairedPeerGate" /> lets only Peers call anything except the pairing calls, which
/// only carry the messages to and from <see cref="PeerPairing" />.
/// </summary>
internal sealed class PeerGrpcService : PeerService.PeerServiceBase
{
    private readonly IConfigurationService _configurationService;
    private readonly PeerConnectionTracker _connections;
    private readonly DeviceIdentity _identity;
    private readonly ILogger<PeerGrpcService> _logger;
    private readonly PeerPairing _pairing;
    private readonly IPeerStore _peerStore;

    public PeerGrpcService(DeviceIdentity identity,
        IConfigurationService configurationService,
        IPeerStore peerStore,
        PeerPairing pairing,
        PeerConnectionTracker connections,
        ILogger<PeerGrpcService> logger)
    {
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _peerStore = peerStore ?? throw new ArgumentNullException(nameof(peerStore));
        _pairing = pairing ?? throw new ArgumentNullException(nameof(pairing));
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private string DeviceName => _configurationService.Configuration.Networking.DeviceName;

    /// <inheritdoc />
    public override Task<HelloReply> Hello(HelloRequest request, ServerCallContext context)
    {
        var reply = new HelloReply
        {
            DeviceId = _identity.DeviceId.Value,
            DeviceName = DeviceName,
            ProtocolMajor = PeerProtocol.Major,
            ProtocolMinor = PeerProtocol.Minor,
        };

        reply.Capabilities.AddRange(PeerProtocol.Capabilities);
        return Task.FromResult(reply);
    }

    /// <inheritdoc />
    public override async Task<PairReply> RequestPairing(PairRequest request, ServerCallContext context)
    {
        var status = await _pairing.AnswerAsync(_identity.DeviceId, PeerCallContext.GetCallerDeviceId(context),
            PeerCallContext.GetRemoteAddress(context), request.DeviceName, request.ProtocolMajor,
            GetCallerAddress(context, request.ListeningPort), context.CancellationToken);

        return Reply(status switch
        {
            PairingStatus.Accepted => PairResult.Accepted,
            PairingStatus.UpdateRequired => PairResult.UpdateRequired,
            PairingStatus.Busy => PairResult.Busy,
            var _ => PairResult.Declined,
        });
    }

    /// <inheritdoc />
    public override async Task<ConfirmPairReply> ConfirmPairing(ConfirmPairRequest request, ServerCallContext context) =>
        new() { Paired = await _pairing.ConfirmAsync(PeerCallContext.GetCallerDeviceId(context), context.CancellationToken) };

    /// <inheritdoc />
    public override async Task<UnpairReply> Unpair(UnpairRequest request, ServerCallContext context)
    {
        var caller = PeerCallContext.GetCallerDeviceId(context);
        _logger.LogInformation("Device {DeviceId} unpaired this Device", caller.ToDisplayString());

        // Removing the Peer drops its connections; let the reply on this one still reach it.
        _connections.CloseGracefullyOnDrop(context.GetHttpContext().Connection.Id);
        await _peerStore.RemoveAsync(caller, context.CancellationToken);
        return new UnpairReply();
    }

    /// <inheritdoc />
    public override Task<CheckPairingReply> CheckPairing(CheckPairingRequest request, ServerCallContext context)
    {
        var caller = PeerCallContext.GetCallerDeviceId(context);
        return Task.FromResult(new CheckPairingReply { Paired = _peerStore.IsPaired(caller) });
    }

    private PairReply Reply(PairResult result) => new()
    {
        Result = result,
        DeviceName = DeviceName,
        ProtocolMajor = PeerProtocol.Major,
        ProtocolMinor = PeerProtocol.Minor,
    };

    private static string? GetCallerAddress(ServerCallContext context, uint listeningPort)
    {
        var address = PeerCallContext.GetRemoteAddress(context);
        return address == null || listeningPort is 0 or > IPEndPoint.MaxPort
            ? null
            : new IPEndPoint(address, (int)listeningPort).ToString();
    }
}
