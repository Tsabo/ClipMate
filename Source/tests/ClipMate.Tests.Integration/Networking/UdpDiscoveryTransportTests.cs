using System.Net;
using System.Net.Sockets;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Networking.Discovery;
using ClipMate.Networking.Discovery.Transport;
using ClipMate.Platform.Services;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClipMate.Tests.Integration.Networking;

/// <summary>
/// Exercises discovery over real UDP sockets, sending only to loopback so nothing leaves this machine.
/// </summary>
[NotInParallel]
public class UdpDiscoveryTransportTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);
    private string _identityDirectory = null!;

    [Before(Test)]
    public void Setup() => _identityDirectory = Path.Combine(Path.GetTempPath(), $"clipmate_discovery_{Guid.NewGuid():N}");

    [After(Test)]
    public void Cleanup()
    {
        if (Directory.Exists(_identityDirectory))
            Directory.Delete(_identityDirectory, true);
    }

    [Test]
    public async Task SendAsync_ToLoopback_IsReceivedOverIPv4AndIPv6()
    {
        // Arrange
        var port = GetFreeUdpPort();
        var options = new UdpDiscoveryTransportOptions
        {
            SendTargets = [new IPEndPoint(IPAddress.Loopback, port), new IPEndPoint(IPAddress.IPv6Loopback, port)],
        };
        await using var transport = new UdpDiscoveryTransportFactory(options, NullLoggerFactory.Instance).Create(port);
        using var timeout = new CancellationTokenSource(_timeout);

        // Act
        await transport.SendAsync("hello"u8.ToArray(), timeout.Token);
        var first = await transport.ReceiveAsync(timeout.Token);
        var second = await transport.ReceiveAsync(timeout.Token);

        // Assert
        await Assert.That(first.Data.ToArray()).IsEquivalentTo("hello"u8.ToArray());
        await Assert.That(second.Data.ToArray()).IsEquivalentTo("hello"u8.ToArray());
        await Assert.That(new[] { first.Source.AddressFamily, second.Source.AddressFamily })
            .IsEquivalentTo([AddressFamily.InterNetwork, AddressFamily.InterNetworkV6]);
    }

    [Test]
    public async Task TwoDevices_OverLoopbackUdp_DiscoverEachOther()
    {
        // Arrange: each Device listens on its own port and sends to the other's, standing in for broadcast
        var laptopPort = GetFreeUdpPort();
        var desktopPort = GetFreeUdpPort();
        using var laptopIdentity = await CreateIdentityAsync("laptop");
        using var desktopIdentity = await CreateIdentityAsync("desktop");
        await using var laptop = CreateService("Laptop", laptopPort, desktopPort);
        await using var desktop = CreateService("Desktop", desktopPort, laptopPort);

        // Act
        await laptop.StartAsync(laptopIdentity, 47823);
        await desktop.StartAsync(desktopIdentity, 47900);
        await WaitUntilAsync(() => laptop.DiscoveredDevices.Count == 1 && desktop.DiscoveredDevices.Count == 1);

        // Assert
        var seenByLaptop = laptop.DiscoveredDevices.Single();
        await Assert.That(seenByLaptop.DeviceId).IsEqualTo(desktopIdentity.DeviceId);
        await Assert.That(seenByLaptop.DeviceName).IsEqualTo("Desktop");
        await Assert.That(seenByLaptop.Endpoint.Port).IsEqualTo(47900);
        await Assert.That(desktop.DiscoveredDevices.Single().DeviceId).IsEqualTo(laptopIdentity.DeviceId);
    }

    [Test]
    public async Task StopAsync_ReleasesTheUdpPort()
    {
        // Arrange
        var port = GetFreeUdpPort();
        using var identity = await CreateIdentityAsync("laptop");
        await using var service = CreateService("Laptop", port, port);
        await service.StartAsync(identity, 47823);

        // Act
        await service.StopAsync();

        // Assert: an exclusive bind succeeds only if nothing holds the port any more
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.ExclusiveAddressUse = true;
        socket.Bind(new IPEndPoint(IPAddress.Any, port));
        await Assert.That(socket.IsBound).IsTrue();
    }

    private static DeviceDiscoveryService CreateService(string deviceName, int listenPort, int sendPort)
    {
        var configuration = new ClipMateConfiguration();
        configuration.Networking.DeviceName = deviceName;
        var configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => configurationService.Configuration).Returns(configuration);

        var transportOptions = new UdpDiscoveryTransportOptions { SendTargets = [new IPEndPoint(IPAddress.Loopback, sendPort)] };
        return new DeviceDiscoveryService(new UdpDiscoveryTransportFactory(transportOptions, NullLoggerFactory.Instance),
            configurationService, NullLogger<DeviceDiscoveryService>.Instance,
            new DeviceDiscoveryOptions { Port = listenPort, AnnouncementInterval = TimeSpan.FromMilliseconds(200) });
    }

    private Task<DeviceIdentity> CreateIdentityAsync(string name) =>
        new DeviceIdentityService(Path.Combine(_identityDirectory, name), NullLogger<DeviceIdentityService>.Instance).GetOrCreateAsync();

    private static int GetFreeUdpPort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(_timeout);
        while (!condition())
            await Task.Delay(20, timeout.Token);
    }
}
