using System.Collections.Concurrent;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using ClipMate.Networking.Protocol;
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
/// Connects to a Peer at one address and exchanges Hello.
/// </summary>
public interface IPeerProbe
{
    /// <summary>
    /// Returns what the Peer reported, or <c>null</c> if it could not be reached there (or is not that Peer).
    /// </summary>
    Task<PeerProbeReply?> ProbeAsync(DeviceIdentity identity, DeviceId peer, string address, CancellationToken cancellationToken);
}

/// <summary>
/// <see cref="IPeerProbe" /> over a Peer connection (<see cref="PeerChannelFactory.CreateForPeer" />).
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
    public async Task<PeerProbeReply?> ProbeAsync(DeviceIdentity identity, DeviceId peer, string address, CancellationToken cancellationToken)
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
            return new PeerProbeReply(reply.DeviceName, reply.ProtocolMajor, reply.ProtocolMinor, reply.Capabilities.ToList());
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(ex, "Peer {DeviceId} not reachable at {Address}", peer.ToShortDisplayString(), address);
            return null;
        }
    }
}

/// <summary>
/// Where to try to reach a Peer.
/// </summary>
internal static class PeerAddresses
{
    /// <summary>
    /// The address the Peer currently announces on the LAN, then its last known addresses (most recent first).
    /// </summary>
    public static IEnumerable<string> GetCandidates(Peer peer, IDeviceDiscoveryService discovery)
    {
        var announced = discovery.DiscoveredDevices.FirstOrDefault(p => p.DeviceId == peer.DeviceId)?.Endpoint.ToString();
        if (announced != null)
            yield return announced;

        foreach (var item in peer.Addresses)
        {
            if (!string.Equals(item, announced, StringComparison.OrdinalIgnoreCase))
                yield return item;
        }
    }
}

/// <summary>
/// Checks every Peer at start, every 30 seconds and whenever the Peers or Discovered Devices change. A Peer is tried at
/// the address it currently announces on the LAN, then at its last known addresses.
/// </summary>
public sealed class PeerStatusMonitor : IPeerStatusService, IAsyncDisposable
{
    private static readonly TimeSpan _interval = TimeSpan.FromSeconds(30);

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
        foreach (var address in PeerAddresses.GetCandidates(peer, _discovery))
        {
            var reply = await _probe.ProbeAsync(identity, peer.DeviceId, address, cancellationToken);
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
