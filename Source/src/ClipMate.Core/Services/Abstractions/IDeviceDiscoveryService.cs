using ClipMate.Core.Models.Networking;

namespace ClipMate.Core.Services.Abstractions;

/// <summary>
/// LAN discovery: while running, announces this Device on the local network and keeps the list of
/// Discovered Devices from the announcements other Devices send. Discovery only reveals Devices; it never grants trust.
/// </summary>
public interface IDeviceDiscoveryService
{
    /// <summary>
    /// Gets whether announcements are being sent and received.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets the Devices currently announcing themselves, excluding this Device. Empty while stopped.
    /// </summary>
    IReadOnlyList<DiscoveredDevice> DiscoveredDevices { get; }

    /// <summary>
    /// Raised (on a background thread) when a Device appears, changes its Device Name or address, or drops off.
    /// </summary>
    event EventHandler? DiscoveredDevicesChanged;

    /// <summary>
    /// Starts announcing <paramref name="identity" /> with its Peer listener on <paramref name="listeningPort" />,
    /// and listening for other Devices' announcements.
    /// </summary>
    Task StartAsync(DeviceIdentity identity, int listeningPort, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops sending and listening for announcements and clears the Discovered Devices.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
