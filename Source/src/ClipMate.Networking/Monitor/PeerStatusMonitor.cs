using System.Collections.Concurrent;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Networking.Monitor.Abstraction;
using Microsoft.Extensions.Logging;

namespace ClipMate.Networking.Monitor;

/// <summary>
/// Checks every Peer at start, every 30 seconds and whenever the Peers or Discovered Devices change, at each of its
/// addresses in turn (<see cref="PeerConnection" />).
/// </summary>
public sealed class PeerStatusMonitor : IPeerStatusService, IAsyncDisposable
{
    private static readonly TimeSpan _interval = TimeSpan.FromSeconds(30);

    // Pairing completes on the approving Device a moment before the requesting Device, which refuses it until then;
    // a Peer this new is not unpaired for saying this Device is not its Peer yet.
    private static readonly TimeSpan _pairingGracePeriod = TimeSpan.FromMinutes(1);

    // While a Peer stays online, its last seen time is written to clipmate.toml at most this often.
    private static readonly TimeSpan _lastSeenPersistInterval = TimeSpan.FromMinutes(10);

    private readonly PeerConnector _connector;
    private readonly IDeviceDiscoveryService _discovery;
    private readonly ILogger<PeerStatusMonitor> _logger;
    private readonly IPeerStore _peerStore;
    private readonly IPeerProbe _probe;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly ConcurrentDictionary<DeviceId, PeerConnectionStatus> _statuses = new();
    private CancellationTokenSource? _cancellation;
    private Task? _loop;

    public PeerStatusMonitor(IPeerStore peerStore, IDeviceDiscoveryService discovery, PeerConnector connector, IPeerProbe probe,
        ILogger<PeerStatusMonitor> logger)
    {
        _peerStore = peerStore ?? throw new ArgumentNullException(nameof(peerStore));
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        _connector = connector ?? throw new ArgumentNullException(nameof(connector));
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await StopAsync();

    /// <inheritdoc />
    public event EventHandler? StatusChanged;

    /// <inheritdoc />
    public PeerConnectionStatus GetStatus(DeviceId deviceId) => _statuses.GetValueOrDefault(deviceId, PeerConnectionStatus.Unknown);

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_cancellation != null)
            throw new InvalidOperationException("The Peer status monitor is already running.");

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
            _loop = null;
            _statuses.Clear();
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var cancellation = _cancellation;
        if (cancellation == null)
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
            await Task.WhenAll(peers.Select(p => CheckAsync(p.DeviceId, linked.Token)));
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

    private void OnPeerRemoved(object? sender, PeerRemovedEventArgs e) => _statuses.TryRemove(e.DeviceId, out var _);

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

    private async Task CheckAsync(DeviceId deviceId, CancellationToken cancellationToken)
    {
        if (_connector.ForPeer(deviceId) is not { } connection)
            return;

        var peer = connection.Peer;
        var answer = await connection.TryEachAddressAsync<PeerProbeResult>(async (address, token) =>
        {
            var result = await _probe.ProbeAsync(connection.Identity, deviceId, address, token);
            return result.Reply != null || (result.Unpaired && !IsCompletingPairing(peer))
                ? result
                : null;
        }, cancellationToken);

        if (answer == null)
        {
            SetStatus(deviceId, PeerConnectionStatus.Offline);
            return;
        }

        if (answer.Value.Reply is not { } reply)
        {
            _logger.LogInformation("Peer {DeviceId} no longer has this Device as a Peer; unpairing it here too", deviceId.ToDisplayString());
            await _peerStore.RemoveAsync(deviceId, cancellationToken);
            return;
        }

        var status = reply.ProtocolMajor == PeerProtocol.Major
            ? PeerConnectionStatus.Online
            : PeerConnectionStatus.UpdateRequired;

        var wasReachable = GetStatus(deviceId) is PeerConnectionStatus.Online or PeerConnectionStatus.UpdateRequired;
        if (!wasReachable || NeedsRecording(peer, reply.DeviceName, answer.Address))
            await _peerStore.RecordSeenAsync(deviceId, reply.DeviceName, answer.Address, DateTime.Now, cancellationToken);

        SetStatus(deviceId, status);
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
