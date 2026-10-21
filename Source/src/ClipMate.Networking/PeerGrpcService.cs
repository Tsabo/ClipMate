using System.Net;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using ClipMate.Networking.Protocol;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace ClipMate.Networking;

/// <summary>
/// Serves the Peer protocol. <see cref="PairedPeerGate" /> lets only Peers call anything except the pairing calls.
/// </summary>
internal sealed class PeerGrpcService : PeerService.PeerServiceBase
{
    private const int _maxDeviceNameLength = 64;
    private readonly IPairingApprovalHandler _approvalHandler;
    private readonly IConfigurationService _configurationService;
    private readonly PeerConnectionTracker _connections;
    private readonly DeviceIdentity _identity;
    private readonly ILogger<PeerGrpcService> _logger;
    private readonly IPeerStore _peerStore;
    private readonly PairingRequestTracker _tracker;

    public PeerGrpcService(DeviceIdentity identity,
        IConfigurationService configurationService,
        IPeerStore peerStore,
        IPairingApprovalHandler approvalHandler,
        PairingRequestTracker tracker,
        PeerConnectionTracker connections,
        ILogger<PeerGrpcService> logger)
    {
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _peerStore = peerStore ?? throw new ArgumentNullException(nameof(peerStore));
        _approvalHandler = approvalHandler ?? throw new ArgumentNullException(nameof(approvalHandler));
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
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
        var caller = PeerCallContext.GetCallerDeviceId(context);
        if (caller == _identity.DeviceId)
            return Reply(PairResult.Declined);

        if (request.ProtocolMajor != PeerProtocol.Major)
        {
            _logger.LogInformation("Pairing request from Device {DeviceId} uses protocol {Major}; update required",
                caller.ToDisplayString(), request.ProtocolMajor);
            return Reply(PairResult.UpdateRequired);
        }

        if (!_tracker.TryBeginWaiting(caller, PeerCallContext.GetRemoteAddress(context)))
            return Reply(PairResult.Busy);

        var accepted = false;
        try
        {
            var deviceName = CleanDeviceName(request.DeviceName, caller);
            var code = VerificationCode.Derive(_identity.DeviceId, caller);
            _logger.LogInformation("Pairing request from {DeviceName} ({DeviceId})", deviceName, caller.ToDisplayString());

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            timeout.CancelAfter(PairingRequestTracker.ApprovalTimeout);

            PairingDecision decision;
            try
            {
                decision = await _approvalHandler.RequestApprovalAsync(new PairingRequest(caller, deviceName, code), timeout.Token);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                decision = PairingDecision.Declined;
            }

            if (!decision.Accepted || context.CancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Pairing request from {DeviceId} was declined", caller.ToDisplayString());
                return Reply(PairResult.Declined);
            }

            _tracker.Accept(caller, deviceName, decision.Alias, GetCallerAddress(context, request.ListeningPort));
            accepted = true;
            return Reply(PairResult.Accepted);
        }
        finally
        {
            _tracker.EndWaiting(caller, accepted);
        }
    }

    /// <inheritdoc />
    public override async Task<ConfirmPairReply> ConfirmPairing(ConfirmPairRequest request, ServerCallContext context)
    {
        var caller = PeerCallContext.GetCallerDeviceId(context);
        if (!_tracker.TryTakeAcceptance(caller, out var acceptance))
        {
            _logger.LogWarning("Ignored a pairing confirmation from Device {DeviceId} that this Device did not accept", caller.ToDisplayString());
            return new ConfirmPairReply { Paired = false };
        }

        await _peerStore.SavePairedAsync(caller, acceptance.DeviceName, acceptance.Alias, acceptance.Address, context.CancellationToken);
        return new ConfirmPairReply { Paired = true };
    }

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

    private static string CleanDeviceName(string deviceName, DeviceId deviceId)
    {
        var name = new string(deviceName.Where(p => !char.IsControl(p)).ToArray()).Trim();
        if (name.Length > _maxDeviceNameLength)
            name = name[.._maxDeviceNameLength];

        return name.Length == 0 ? deviceId.ToShortDisplayString() : name;
    }

    private static string? GetCallerAddress(ServerCallContext context, uint listeningPort)
    {
        var address = PeerCallContext.GetRemoteAddress(context);
        return address == null || listeningPort is 0 or > IPEndPoint.MaxPort
            ? null
            : new IPEndPoint(address, (int)listeningPort).ToString();
    }
}
