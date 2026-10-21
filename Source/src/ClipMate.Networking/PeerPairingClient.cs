using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using ClipMate.Networking.Protocol;
using Grpc.Core;
using Grpc.Net.Client;
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

/// <summary>
/// One outgoing pairing request: sends it at once, then completes Pairing once both users have accepted.
/// </summary>
internal sealed class PairingSession : IPairingSession
{
    private static readonly TimeSpan _confirmTimeout = TimeSpan.FromSeconds(15);

    // Slightly longer than the other Device waits for its user, so its answer (or timeout) arrives first.
    private static readonly TimeSpan _requestTimeout = PairingRequestTracker.ApprovalTimeout + TimeSpan.FromSeconds(15);

    private readonly CancellationTokenSource _cancellation = new();
    private readonly GrpcChannel _channel;
    private readonly PeerService.PeerServiceClient _client;
    private readonly ILogger<PairingSession> _logger;
    private readonly IPeerStore _peerStore;
    private int _accepted;
    private string? _peerDeviceName;
    private int _wrongDevice;

    public PairingSession(DeviceIdentity identity, string deviceName, int listeningPort, PairingTarget target, IPeerStore peerStore,
        ILogger<PairingSession> logger)
    {
        Target = target;
        _peerStore = peerStore;
        _logger = logger;
        VerificationCode = Core.Models.Networking.VerificationCode.Derive(identity.DeviceId, target.DeviceId);
        _channel = PeerChannelFactory.CreateForPairing(target.Address, identity, target.DeviceId, OnWrongDevice);
        _client = new PeerService.PeerServiceClient(_channel);
        PeerResponse = RequestAsync(deviceName, listeningPort);
    }

    /// <inheritdoc />
    public PairingTarget Target { get; }

    /// <inheritdoc />
    public string VerificationCode { get; }

    /// <inheritdoc />
    public string? PeerDeviceName => Volatile.Read(ref _peerDeviceName);

    /// <inheritdoc />
    public Task<PairingStatus> PeerResponse { get; }

    /// <inheritdoc />
    public async Task<PairingStatus> AcceptAsync(string? alias, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _accepted, 1) != 0)
            throw new InvalidOperationException("This pairing request has already been accepted.");

        if (_cancellation.IsCancellationRequested)
            return PairingStatus.Cancelled;

        var response = await PeerResponse.WaitAsync(cancellationToken);
        if (response != PairingStatus.Accepted)
            return response;

        if (_cancellation.IsCancellationRequested)
            return PairingStatus.Cancelled;

        try
        {
            var reply = await _client.ConfirmPairingAsync(new ConfirmPairRequest(), deadline: DateTime.UtcNow + _confirmTimeout,
                cancellationToken: cancellationToken);
            if (!reply.Paired)
            {
                _logger.LogWarning("Device {DeviceId} did not complete Pairing (its acceptance expired)", Target.DeviceId.ToDisplayString());
                return PairingStatus.TimedOut;
            }
        }
        catch (RpcException ex)
        {
            return ToStatus(ex);
        }

        var deviceName = PeerDeviceName ?? (string.IsNullOrWhiteSpace(Target.DeviceName) ? Target.Address : Target.DeviceName);
        await _peerStore.SavePairedAsync(Target.DeviceId, deviceName, alias, Target.Address, Target.AddressEnteredManually, CancellationToken.None);
        return PairingStatus.Paired;
    }

    /// <inheritdoc />
    public void Cancel()
    {
        try
        {
            _cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed: nothing left to cancel.
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Cancel();
        try
        {
            await PeerResponse;
        }
        finally
        {
            _channel.Dispose();
            _cancellation.Dispose();
        }
    }

    private async Task<PairingStatus> RequestAsync(string deviceName, int listeningPort)
    {
        await Task.Yield(); // never run the network call on the constructor's thread
        try
        {
            var reply = await _client.RequestPairingAsync(new PairRequest
            {
                DeviceName = deviceName,
                ProtocolMajor = PeerProtocol.Major,
                ProtocolMinor = PeerProtocol.Minor,
                ListeningPort = (uint)Math.Max(0, listeningPort),
            }, deadline: DateTime.UtcNow + _requestTimeout, cancellationToken: _cancellation.Token);

            Volatile.Write(ref _peerDeviceName, string.IsNullOrWhiteSpace(reply.DeviceName) ? null : reply.DeviceName.Trim());
            return reply.Result switch
            {
                PairResult.Accepted => PairingStatus.Accepted,
                PairResult.Declined => PairingStatus.Declined,
                PairResult.UpdateRequired => PairingStatus.UpdateRequired,
                PairResult.Busy => PairingStatus.Busy,
                var _ => PairingStatus.Failed,
            };
        }
        catch (RpcException ex)
        {
            return ToStatus(ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Pairing request to {Address} failed", Target.Address);
            return IsWrongDevice ? PairingStatus.WrongDevice : PairingStatus.Unreachable;
        }
        catch (OperationCanceledException)
        {
            return PairingStatus.Cancelled;
        }
    }

    private bool IsWrongDevice => Volatile.Read(ref _wrongDevice) != 0;

    private void OnWrongDevice(DeviceId presented)
    {
        if (Interlocked.Exchange(ref _wrongDevice, 1) == 0)
        {
            _logger.LogWarning("The Device at {Address} has Device ID {PresentedDeviceId}, not {ExpectedDeviceId}; connection refused",
                Target.Address, presented.ToDisplayString(), Target.DeviceId.ToDisplayString());
        }
    }

    private PairingStatus ToStatus(RpcException exception)
    {
        if (_cancellation.IsCancellationRequested || exception.StatusCode == StatusCode.Cancelled)
            return PairingStatus.Cancelled;

        if (IsWrongDevice)
            return PairingStatus.WrongDevice;

        _logger.LogWarning("Pairing with {Address} failed: {Status}", Target.Address, exception.Status);
        return exception.StatusCode switch
        {
            StatusCode.Unavailable => PairingStatus.Unreachable,

            // The connection failed, e.g. the TLS handshake rejected a server that is not the expected Device.
            StatusCode.Internal when exception.Status.DebugException is HttpRequestException => PairingStatus.Unreachable,
            StatusCode.DeadlineExceeded => PairingStatus.TimedOut,
            StatusCode.Unimplemented => PairingStatus.UpdateRequired,
            var _ => PairingStatus.Failed,
        };
    }
}
