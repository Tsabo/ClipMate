using ClipMate.Core.Models;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Models.Search;
using ClipMate.Core.Services;
using Microsoft.Extensions.Logging;

namespace ClipMate.Networking.SharedDatabases;

/// <summary>
/// <see cref="IRemoteDatabaseService" /> built from the Peer records and Peer status.
/// <para>
/// Each Peer's share list is fetched when it comes online and every 30 seconds while it stays online, and remembered in
/// its Peer record, so its Remote Databases stay listed (as unavailable) while it is offline, even after a restart.
/// Nothing here ever becomes a database configuration entry.
/// </para>
/// </summary>
public sealed class RemoteDatabaseService : IRemoteDatabaseService, IDisposable
{
    private static readonly TimeSpan _refreshInterval = TimeSpan.FromSeconds(30);
    private readonly ISharedDatabaseClient _client;
    private readonly IConfigurationService _configurationService;
    private readonly ILocalDevice _localDevice;
    private readonly ILogger<RemoteDatabaseService> _logger;
    private readonly IPeerStore _peerStore;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly Lock _snapshotLock = new();
    private readonly IPeerStatusService _status;
    private readonly ITimer _timer;
    private bool _disposed;
    private IReadOnlyList<RemoteDatabase> _snapshot = [];

    public RemoteDatabaseService(IPeerStore peerStore,
        IPeerStatusService status,
        ILocalDevice localDevice,
        IConfigurationService configurationService,
        ISharedDatabaseClient client,
        TimeProvider timeProvider,
        ILogger<RemoteDatabaseService> logger)
    {
        _peerStore = peerStore ?? throw new ArgumentNullException(nameof(peerStore));
        _status = status ?? throw new ArgumentNullException(nameof(status));
        _localDevice = localDevice ?? throw new ArgumentNullException(nameof(localDevice));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentNullException.ThrowIfNull(timeProvider);

        _snapshot = BuildSnapshot();
        _peerStore.PeersChanged += OnPeersChanged;
        _status.StatusChanged += OnStatusChanged;

        // Does nothing while networking is off or no Peer is online.
        _timer = timeProvider.CreateTimer(_ => _ = RefreshSafelyAsync(), null, _refreshInterval, _refreshInterval);
    }

    /// <inheritdoc />
    public event EventHandler? RemoteDatabasesChanged;

    /// <inheritdoc />
    public IReadOnlyList<RemoteDatabase> GetRemoteDatabases()
    {
        lock (_snapshotLock)
            return _snapshot;
    }

    /// <inheritdoc />
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            var identity = _localDevice.Identity;
            if (_configurationService.Configuration.Networking.Enabled && identity != null)
            {
                var online = _peerStore.GetPeers().Where(p => _status.GetStatus(p.DeviceId) == PeerConnectionStatus.Online);
                await Task.WhenAll(online.Select(p => RefreshPeerAsync(identity, p, cancellationToken)));
            }
        }
        finally
        {
            _refreshGate.Release();
        }

        PublishIfChanged();
    }

    /// <inheritdoc />
    public async Task SetAliasAsync(RemoteDatabaseAddress address, string? alias, CancellationToken cancellationToken = default)
    {
        await _peerStore.SetDatabaseAliasAsync(address.DeviceId, address.DatabaseId, alias, cancellationToken);

        // Show the new label now rather than when the PeersChanged handler gets to it
        PublishIfChanged();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SharedCollection>> GetCollectionTreeAsync(RemoteDatabaseAddress address,
        CancellationToken cancellationToken = default)
    {
        var (identity, peer) = GetReachablePeer(address.DeviceId);
        return await _client.GetCollectionTreeAsync(identity, peer, address.DatabaseId, cancellationToken)
               ?? throw new RemoteDatabaseUnavailableException($"{peer.DisplayName} no longer shares this Database.");
    }

    /// <inheritdoc />
    public async Task<ClipPage> GetClipPageAsync(RemoteDatabaseAddress address, Guid collectionId, Guid? folderId, int offset, int count,
        CancellationToken cancellationToken = default)
    {
        var (identity, peer) = GetReachablePeer(address.DeviceId);
        return await _client.GetClipPageAsync(identity, peer, address.DatabaseId, collectionId, folderId, offset, count, cancellationToken)
               ?? throw new RemoteDatabaseUnavailableException($"{peer.DisplayName} no longer shares this collection.");
    }

    /// <inheritdoc />
    public async Task<ClipContent?> GetClipContentAsync(RemoteDatabaseAddress address, Guid clipId, CancellationToken cancellationToken = default)
    {
        var (identity, peer) = GetReachablePeer(address.DeviceId);
        return await _client.GetClipContentAsync(identity, peer, address.DatabaseId, clipId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SearchMatches> SearchAsync(RemoteDatabaseAddress address, SearchFilters filters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filters);
        var (identity, peer) = GetReachablePeer(address.DeviceId);
        return await _client.SearchAsync(identity, peer, address.DatabaseId, filters, cancellationToken)
               ?? throw new RemoteDatabaseUnavailableException($"{peer.DisplayName} no longer shares this Database or collection.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _peerStore.PeersChanged -= OnPeersChanged;
        _status.StatusChanged -= OnStatusChanged;
        _timer.Dispose();
    }

    private (DeviceIdentity Identity, Peer Peer) GetReachablePeer(DeviceId deviceId)
    {
        var identity = _localDevice.Identity;
        if (!_configurationService.Configuration.Networking.Enabled || identity == null)
            throw new RemoteDatabaseUnavailableException("Networking is off.");

        var peer = _peerStore.Find(deviceId) ?? throw new RemoteDatabaseUnavailableException("That Device is not a Peer.");
        if (_status.GetStatus(deviceId) != PeerConnectionStatus.Online)
            throw new RemoteDatabaseUnavailableException($"{peer.DisplayName} is offline.");

        return (identity, peer);
    }

    private async Task RefreshPeerAsync(DeviceIdentity identity, Peer peer, CancellationToken cancellationToken)
    {
        try
        {
            var shared = await _client.ListSharedDatabasesAsync(identity, peer, cancellationToken);
            if (!shared.SequenceEqual(peer.SharedDatabases))
                await _peerStore.RecordSharedDatabasesAsync(peer.DeviceId, shared, cancellationToken);
        }
        catch (RemoteDatabaseUnavailableException ex)
        {
            // The status monitor notices separately; keep the last known list.
            _logger.LogDebug(ex, "Could not get the Shared Databases of Peer {DeviceId}", peer.DeviceId.ToShortDisplayString());
        }
    }

    private void OnPeersChanged(object? sender, EventArgs e) => _ = Task.Run(PublishIfChanged);

    private void OnStatusChanged(object? sender, EventArgs e) => _ = RefreshSafelyAsync();

    private async Task RefreshSafelyAsync()
    {
        if (_disposed)
            return;

        try
        {
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh Remote Databases");
        }
    }

    private void PublishIfChanged()
    {
        var snapshot = BuildSnapshot();
        lock (_snapshotLock)
        {
            if (snapshot.SequenceEqual(_snapshot))
                return;

            _snapshot = snapshot;
        }

        RemoteDatabasesChanged?.Invoke(this, EventArgs.Empty);
    }

    private IReadOnlyList<RemoteDatabase> BuildSnapshot()
    {
        if (!_configurationService.Configuration.Networking.Enabled)
            return [];

        return _peerStore.GetPeers()
            .SelectMany(peer => peer.SharedDatabases.Select(database => new RemoteDatabase(
                new RemoteDatabaseAddress(peer.DeviceId, database.DatabaseId),
                database.Name,
                peer.DisplayName,
                _status.GetStatus(peer.DeviceId),
                peer.LastSeen) { Alias = peer.GetDatabaseAlias(database.DatabaseId) }))
            .OrderBy(p => p.PeerName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
