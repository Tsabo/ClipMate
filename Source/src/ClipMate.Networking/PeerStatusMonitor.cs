using System.Collections.Concurrent;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using System.Net.Sockets;
using ClipMate.Networking.Protocol;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace ClipMate.Networking;

/// <summary>
/// What a Peer reported about itself in Hello.
/// </summary>
/// <param name="DeviceName">The Peer's current Device Name.</param>
/// <param name="ProtocolMajor">The Peer's protocol major version.</param>
/// <param name="ProtocolMinor">The Peer's protocol minor version.</param>
/// <param name="Capabilities">The optional features the Peer supports.</param>
public sealed record PeerProbeReply(string DeviceName, uint ProtocolMajor, uint ProtocolMinor, IReadOnlyList<string> Capabilities);

/// <summary>
/// The outcome of probing a Peer at one address.
/// </summary>
/// <param name="Reply">What the Peer reported in Hello, or <c>null</c> if it did not answer.</param>
/// <param name="Unpaired">
/// <c>true</c> if the Device answered that this Device is not its Peer (it unpaired this Device, or its Pairing never completed there).
/// </param>
public sealed record PeerProbeResult(PeerProbeReply? Reply, bool Unpaired)
{
    /// <summary>
    /// The Peer could not be reached there (or is not that Device).
    /// </summary>
    public static PeerProbeResult Unreachable { get; } = new(null, false);

    /// <summary>
    /// The Device is no longer paired with this Device.
    /// </summary>
    public static PeerProbeResult NotPaired { get; } = new(null, true);

    /// <summary>
    /// The Peer answered Hello with <paramref name="reply" />.
    /// </summary>
    public static PeerProbeResult Reached(PeerProbeReply reply) => new(reply, false);
}

/// <summary>
/// Connects to a Peer at one address and exchanges Hello.
/// </summary>
public interface IPeerProbe
{
    /// <summary>
    /// Returns what the Peer reported; if it refused this Device, whether that is because this Device is no longer its Peer.
    /// </summary>
    Task<PeerProbeResult> ProbeAsync(DeviceIdentity identity, DeviceId peer, string address, CancellationToken cancellationToken);
}

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
            return reply.Paired ? PeerProbeResult.Unreachable : PeerProbeResult.NotPaired;
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

/// <summary>
/// Checks every Peer at start, every 30 seconds and whenever the Peers or Discovered Devices change. A Peer is tried at
/// the address it currently announces on the LAN, then at its last known addresses.
/// </summary>
public sealed class PeerStatusMonitor : IPeerStatusService, IAsyncDisposable
{
    private static readonly TimeSpan _interval = TimeSpan.FromSeconds(30);

    // Pairing completes on the approving Device a moment before the requesting Device, which refuses it until then;
    // a Peer this new is not unpaired for saying this Device is not its Peer yet.
    private static readonly TimeSpan _pairingGracePeriod = TimeSpan.FromMinutes(1);

    // While a Peer stays online, its last seen time is written to clipmate.toml at most this often.
    private static readonly TimeSpan _lastSeenPersistInterval = TimeSpan.FromMinutes(10);

    private readonly IDeviceDiscoveryService _discovery;
    private readonly ILogger<PeerStatusMonitor> _logger;
    private readonly IPeerStore _peerStore;
    private readonly IPeerProbe _probe;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly ConcurrentDictionary<DeviceId, PeerConnectionStatus> _statuses = new();
    private CancellationTokenSource? _cancellation;
    private DeviceIdentity? _identity;
    private Task? _loop;

    public PeerStatusMonitor(IPeerStore peerStore, IDeviceDiscoveryService discovery, IPeerProbe probe, ILogger<PeerStatusMonitor> logger)
    {
        _peerStore = peerStore ?? throw new ArgumentNullException(nameof(peerStore));
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public event EventHandler? StatusChanged;

    /// <inheritdoc />
    public PeerConnectionStatus GetStatus(DeviceId deviceId) => _statuses.GetValueOrDefault(deviceId, PeerConnectionStatus.Unknown);

    /// <inheritdoc />
    public Task StartAsync(DeviceIdentity identity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (_cancellation != null)
            throw new InvalidOperationException("The Peer status monitor is already running.");

        _identity = identity;
        _cancellation = new CancellationTokenSource();
        _peerStore.PeersChanged += OnChanged;
        _peerStore.PeerRemoved += OnPeerRemoved;
        _discovery.DiscoveredDevicesChanged += OnChanged;
        _loop = RunAsync(_cancellation.Token);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        var cancellation = _cancellation;
        if (cancellation == null)
            return;

        _peerStore.PeersChanged -= OnChanged;
        _peerStore.PeerRemoved -= OnPeerRemoved;
        _discovery.DiscoveredDevicesChanged -= OnChanged;
        await cancellation.CancelAsync();
        try
        {
            if (_loop != null)
                await _loop;

            // Wait for a refresh started from outside the loop.
            await _refreshGate.WaitAsync(cancellationToken);
            _refreshGate.Release();
        }
        finally
        {
            _cancellation = null;
            _identity = null;
            _loop = null;
            _statuses.Clear();
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var identity = _identity;
        var cancellation = _cancellation;
        if (identity == null || cancellation == null)
            return;

        // The stop source is never disposed (it owns no timer), so linking to it is always safe.
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cancellation.Token);
        try
        {
            await _refreshGate.WaitAsync(linked.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            var peers = _peerStore.GetPeers();
            await Task.WhenAll(peers.Select(p => CheckAsync(identity, p, linked.Token)));
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            // Stopped or cancelled.
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await StopAsync();

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        using var timer = new PeriodicTimer(_interval);
        try
        {
            do
            {
                await RefreshAsync(cancellationToken);
            } while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException)
        {
            // Stopped.
        }
    }

    private void OnChanged(object? sender, EventArgs e) => _ = RefreshSafelyAsync();

    private void OnPeerRemoved(object? sender, PeerRemovedEventArgs e) => _statuses.TryRemove(e.DeviceId, out _);

    private async Task RefreshSafelyAsync()
    {
        try
        {
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check Peer status");
        }
    }

    private async Task CheckAsync(DeviceIdentity identity, Peer peer, CancellationToken cancellationToken)
    {
        foreach (var address in PeerAddress.GetAddressesToTry(peer, _discovery.DiscoveredDevices))
        {
            var result = await _probe.ProbeAsync(identity, peer.DeviceId, address, cancellationToken);
            if (result.Unpaired && !IsCompletingPairing(peer))
            {
                _logger.LogInformation("Peer {DeviceId} no longer has this Device as a Peer; unpairing it here too", peer.DeviceId.ToDisplayString());
                await _peerStore.RemoveAsync(peer.DeviceId, cancellationToken);
                return;
            }

            var reply = result.Reply;
            if (reply == null)
                continue;

            var status = reply.ProtocolMajor == PeerProtocol.Major ? PeerConnectionStatus.Online : PeerConnectionStatus.UpdateRequired;
            var wasReachable = GetStatus(peer.DeviceId) is PeerConnectionStatus.Online or PeerConnectionStatus.UpdateRequired;
            if (!wasReachable || NeedsRecording(peer, reply.DeviceName, address))
                await _peerStore.RecordSeenAsync(peer.DeviceId, reply.DeviceName, address, DateTime.Now, cancellationToken);

            SetStatus(peer.DeviceId, status);
            return;
        }

        SetStatus(peer.DeviceId, PeerConnectionStatus.Offline);
    }

    private static bool IsCompletingPairing(Peer peer) =>
        peer.PairedAt is { } pairedAt && DateTime.Now - pairedAt < _pairingGracePeriod;

    private static bool NeedsRecording(Peer peer, string deviceName, string address) =>
        peer.DeviceName != deviceName
        || peer.Addresses.Count == 0
        || !string.Equals(peer.Addresses[0], address, StringComparison.OrdinalIgnoreCase)
        || peer.LastSeen == null
        || DateTime.Now - peer.LastSeen.Value > _lastSeenPersistInterval;

    private void SetStatus(DeviceId deviceId, PeerConnectionStatus status)
    {
        var previous = _statuses.GetValueOrDefault(deviceId, PeerConnectionStatus.Unknown);
        _statuses[deviceId] = status;
        if (previous != status)
        {
            _logger.LogInformation("Peer {DeviceId} is {Status}", deviceId.ToShortDisplayString(), status);
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
