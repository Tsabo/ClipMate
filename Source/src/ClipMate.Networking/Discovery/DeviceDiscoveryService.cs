using System.Net;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using Microsoft.Extensions.Logging;

namespace ClipMate.Networking.Discovery;

/// <summary>
/// Settings for LAN discovery that are not user configuration.
/// </summary>
public sealed class DeviceDiscoveryOptions
{
    /// <summary>
    /// Default UDP port for discovery announcements; distinct from the Peer listener's TCP port.
    /// </summary>
    public const int DefaultPort = 47824;

    /// <summary>
    /// Gets the UDP port announcements are sent to and received on.
    /// </summary>
    public int Port { get; init; } = DefaultPort;

    /// <summary>
    /// Gets how often this Device announces itself.
    /// </summary>
    public TimeSpan AnnouncementInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets how long a Discovered Device stays listed after its latest announcement (a little over three missed announcements).
    /// </summary>
    public TimeSpan ExpiryTime { get; init; } = TimeSpan.FromSeconds(95);

    /// <summary>
    /// Gets how often expired Discovered Devices are removed.
    /// </summary>
    public TimeSpan SweepInterval { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets the most Discovered Devices kept; announcements from further Devices are ignored.
    /// </summary>
    public int MaxDiscoveredDevices { get; init; } = 256;
}

/// <summary>
/// Announces this Device on the LAN and lists the other Devices that announce themselves (spec section 5.2).
/// Only announcements with a valid signature from the certificate behind the claimed Device ID are accepted.
/// </summary>
public sealed class DeviceDiscoveryService : IDeviceDiscoveryService, IAsyncDisposable, IDisposable
{
    private readonly IConfigurationService _configurationService;
    private readonly Dictionary<DeviceId, DiscoveredDevice> _devices = [];
    private readonly Lock _lock = new();
    private readonly ILogger<DeviceDiscoveryService> _logger;
    private readonly DeviceDiscoveryOptions _options;
    private readonly SemaphoreSlim _startStopGate = new(1, 1);
    private readonly TimeProvider _timeProvider;
    private readonly IDiscoveryTransportFactory _transportFactory;
    private CancellationTokenSource? _cancellation;
    private Task? _loops;
    private IDiscoveryTransport? _transport;

    public DeviceDiscoveryService(IDiscoveryTransportFactory transportFactory,
        IConfigurationService configurationService,
        ILogger<DeviceDiscoveryService> logger,
        DeviceDiscoveryOptions? options = null,
        TimeProvider? timeProvider = null)
    {
        _transportFactory = transportFactory ?? throw new ArgumentNullException(nameof(transportFactory));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new DeviceDiscoveryOptions();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public bool IsRunning => _transport != null;

    /// <inheritdoc />
    public IReadOnlyList<DiscoveredDevice> DiscoveredDevices
    {
        get
        {
            var cutoff = _timeProvider.GetUtcNow() - _options.ExpiryTime;
            lock (_lock)
            {
                return _devices.Values
                    .Where(p => p.LastSeen > cutoff)
                    .OrderBy(p => p.DeviceName, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler? DiscoveredDevicesChanged;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _startStopGate.Dispose();
    }

    /// <inheritdoc />
    public void Dispose() => Task.Run(async () => await DisposeAsync()).GetAwaiter().GetResult();

    /// <inheritdoc />
    public async Task StartAsync(DeviceIdentity identity, int listeningPort, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        await _startStopGate.WaitAsync(cancellationToken);
        if (_transport != null)
        {
            _startStopGate.Release();
            throw new InvalidOperationException("Discovery is already running.");
        }

        try
        {
            var transport = _transportFactory.Create(_options.Port);
            _transport = transport;
            _cancellation = new CancellationTokenSource();
            var token = _cancellation.Token;

            // Announce right away so other Devices see this one without waiting a full interval.
            await AnnounceAsync(transport, identity, listeningPort, token);

            _loops = Task.WhenAll(
                Task.Run(() => AnnounceLoopAsync(transport, identity, listeningPort, token), CancellationToken.None),
                Task.Run(() => ReceiveLoopAsync(transport, identity.DeviceId, token), CancellationToken.None),
                Task.Run(() => SweepLoopAsync(token), CancellationToken.None));

            _logger.LogInformation("LAN discovery started on UDP port {Port}", _options.Port);
        }
        catch
        {
            await StopCoreAsync();
            throw;
        }
        finally
        {
            _startStopGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _startStopGate.WaitAsync(cancellationToken);
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        try
        {
            await StopCoreAsync();
        }
        finally
        {
            _startStopGate.Release();
        }
    }

    /// <summary>
    /// Removes Discovered Devices whose announcements have stopped, and reports whether any were removed.
    /// </summary>
    internal bool RemoveExpired()
    {
        var cutoff = _timeProvider.GetUtcNow() - _options.ExpiryTime;
        List<DiscoveredDevice> expired;
        lock (_lock)
        {
            expired = _devices.Values.Where(p => p.LastSeen <= cutoff).ToList();
            foreach (var item in expired)
                _devices.Remove(item.DeviceId);
        }

        foreach (var item in expired)
            _logger.LogInformation("Discovered Device {DeviceName} ({DeviceId}) stopped announcing", item.DeviceName, item.DeviceId.ToShortDisplayString());

        return expired.Count > 0;
    }

    private async Task StopCoreAsync()
    {
        var transport = _transport;
        if (transport == null)
            return;

        _transport = null;
        if (_cancellation != null)
            await _cancellation.CancelAsync();

        if (_loops != null)
        {
            try
            {
                await _loops;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "LAN discovery stopped with an error");
            }
            catch (OperationCanceledException)
            {
                // Expected.
            }
        }

        await transport.DisposeAsync();
        _cancellation?.Dispose();
        _cancellation = null;
        _loops = null;

        bool hadDevices;
        lock (_lock)
        {
            hadDevices = _devices.Count > 0;
            _devices.Clear();
        }

        _logger.LogInformation("LAN discovery stopped");
        if (hadDevices)
            RaiseChanged();
    }

    private async Task AnnounceLoopAsync(IDiscoveryTransport transport, DeviceIdentity identity, int listeningPort, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(_options.AnnouncementInterval, _timeProvider, cancellationToken);
            await AnnounceAsync(transport, identity, listeningPort, cancellationToken);
        }
    }

    private async Task AnnounceAsync(IDiscoveryTransport transport, DeviceIdentity identity, int listeningPort, CancellationToken cancellationToken)
    {
        try
        {
            var datagram = DiscoveryAnnouncement.Encode(identity, _configurationService.Configuration.Networking.DeviceName, listeningPort);
            await transport.SendAsync(datagram, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to send the LAN discovery announcement");
        }
    }

    private async Task ReceiveLoopAsync(IDiscoveryTransport transport, DeviceId ownDeviceId, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            DiscoveryDatagram datagram;
            try
            {
                datagram = await transport.ReceiveAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to receive a LAN discovery announcement");
                await Task.Delay(TimeSpan.FromSeconds(1), _timeProvider, cancellationToken);
                continue;
            }

            HandleDatagram(datagram, ownDeviceId);
        }
    }

    private void HandleDatagram(DiscoveryDatagram datagram, DeviceId ownDeviceId)
    {
        if (!DiscoveryAnnouncement.TryDecode(datagram.Data.Span, out var announcement))
        {
            _logger.LogDebug("Ignored an invalid LAN discovery datagram from {Source}", datagram.Source);
            return;
        }

        if (announcement.DeviceId == ownDeviceId)
            return;

        var device = new DiscoveredDevice(announcement.DeviceId, announcement.DeviceName,
            new IPEndPoint(datagram.Source.Address, announcement.Port),
            announcement.ProtocolMajor, announcement.ProtocolMinor, _timeProvider.GetUtcNow());

        bool changed;
        lock (_lock)
        {
            if (_devices.TryGetValue(device.DeviceId, out var existing))
                changed = existing.DeviceName != device.DeviceName || !existing.Endpoint.Equals(device.Endpoint)
                                                                   || existing.ProtocolMajor != device.ProtocolMajor
                                                                   || existing.ProtocolMinor != device.ProtocolMinor;
            else if (_devices.Count >= _options.MaxDiscoveredDevices)
                return;
            else
                changed = true;

            _devices[device.DeviceId] = device;
        }

        if (!changed)
            return;

        _logger.LogInformation("Discovered Device {DeviceName} ({DeviceId}) at {Endpoint}", device.DeviceName, device.DeviceId.ToShortDisplayString(), device.Endpoint);
        RaiseChanged();
    }

    private async Task SweepLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(_options.SweepInterval, _timeProvider, cancellationToken);
            if (RemoveExpired())
                RaiseChanged();
        }
    }

    private void RaiseChanged()
    {
        try
        {
            DiscoveredDevicesChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "A DiscoveredDevicesChanged handler failed");
        }
    }
}
