using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Networking.Discovery;
using ClipMate.Networking.Discovery.Abstraction;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClipMate.Tests.Unit.Networking;

public class DeviceDiscoveryServiceTests
{
    private static readonly IPAddress _laptopAddress = IPAddress.Parse("192.168.1.10");
    private static readonly IPAddress _desktopAddress = IPAddress.Parse("192.168.1.20");
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    private readonly List<DeviceDiscoveryService> _services = [];
    private readonly List<DeviceIdentity> _identities = [];
    private ManualTimeProvider _clock = null!;
    private InMemoryDiscoveryNetwork _network = null!;

    [Before(Test)]
    public void Setup()
    {
        _network = new InMemoryDiscoveryNetwork();
        _clock = new ManualTimeProvider();
    }

    [After(Test)]
    public async Task Cleanup()
    {
        foreach (var item in _services)
            await item.StopAsync();

        foreach (var item in _identities)
            item.Dispose();
    }

    [Test]
    public async Task StartAsync_AnnouncesDeviceIdNameAndListeningPort()
    {
        // Arrange
        var (laptop, identity) = CreateDevice("Laptop", _laptopAddress);

        // Act
        await laptop.StartAsync(identity, 47823);

        // Assert
        await Assert.That(_network.Sent.IsEmpty).IsFalse();
        await Assert.That(DiscoveryAnnouncement.TryDecode(_network.Sent.First().Data.Span, out var announcement)).IsTrue();
        await Assert.That(announcement!.DeviceId).IsEqualTo(identity.DeviceId);
        await Assert.That(announcement.DeviceName).IsEqualTo("Laptop");
        await Assert.That(announcement.Port).IsEqualTo(47823);
    }

    [Test]
    public async Task StartAsync_KeepsAnnouncingAtTheAnnouncementInterval()
    {
        // Arrange
        var (laptop, identity) = CreateDevice("Laptop", _laptopAddress);
        await laptop.StartAsync(identity, 47823);
        var sentAtStart = _network.Sent.Count;
        await WaitForTimersAsync(1);

        // Act: just short of the interval, then the rest of it
        _clock.Advance(TestOptions.AnnouncementInterval - TimeSpan.FromSeconds(1));
        var sentBeforeTheInterval = _network.Sent.Count;
        _clock.Advance(TimeSpan.FromSeconds(1));

        // Assert
        await Assert.That(sentAtStart).IsEqualTo(1); // announced right away
        await Assert.That(sentBeforeTheInterval).IsEqualTo(1);
        await WaitUntilAsync(() => _network.Sent.Count == 2);
    }

    [Test]
    public async Task TwoDevices_SeeEachOtherButNotThemselves()
    {
        // Arrange
        var (laptop, laptopIdentity) = CreateDevice("Laptop", _laptopAddress);
        var (desktop, desktopIdentity) = CreateDevice("Desktop", _desktopAddress);

        // Act
        await laptop.StartAsync(laptopIdentity, 47823);
        await desktop.StartAsync(desktopIdentity, 50000);
        await WaitForTimersAsync(2);
        _clock.Advance(TestOptions.AnnouncementInterval); // the laptop's next announcement reaches the desktop, which started later
        await WaitUntilAsync(() => laptop.DiscoveredDevices.Count == 1 && desktop.DiscoveredDevices.Count == 1);

        // Assert
        var seenByLaptop = laptop.DiscoveredDevices.Single();
        await Assert.That(seenByLaptop.DeviceId).IsEqualTo(desktopIdentity.DeviceId);
        await Assert.That(seenByLaptop.DeviceName).IsEqualTo("Desktop");
        await Assert.That(seenByLaptop.Endpoint).IsEqualTo(new IPEndPoint(_desktopAddress, 50000));
        await Assert.That(desktop.DiscoveredDevices.Single().DeviceId).IsEqualTo(laptopIdentity.DeviceId);
    }

    [Test]
    public async Task ReceivingAnnouncement_RaisesDiscoveredDevicesChanged()
    {
        // Arrange
        var (laptop, laptopIdentity) = CreateDevice("Laptop", _laptopAddress);
        var desktopIdentity = CreateIdentity();
        var changed = 0;
        laptop.DiscoveredDevicesChanged += (_, _) => Interlocked.Increment(ref changed);
        await laptop.StartAsync(laptopIdentity, 47823);

        // Act
        _network.Deliver(DiscoveryAnnouncement.Encode(desktopIdentity, "Desktop", 47823), new IPEndPoint(_desktopAddress, 47824));

        // Assert
        await WaitUntilAsync(() => Volatile.Read(ref changed) > 0);
    }

    [Test]
    public async Task ForgedAnnouncement_IsIgnored()
    {
        // Arrange
        var (laptop, laptopIdentity) = CreateDevice("Laptop", _laptopAddress);
        var desktopIdentity = CreateIdentity();
        var victimIdentity = CreateIdentity();
        await laptop.StartAsync(laptopIdentity, 47823);

        // Act: claim the victim's Device ID while carrying (and signing with) the desktop's certificate
        _network.Deliver(DiscoveryAnnouncement.Encode(desktopIdentity, "Victim", 47823, claimedDeviceId: victimIdentity.DeviceId),
            new IPEndPoint(_desktopAddress, 47824));
        _network.Deliver(DiscoveryAnnouncement.Encode(desktopIdentity, "Desktop", 47823), new IPEndPoint(_desktopAddress, 47824));
        await WaitUntilAsync(() => laptop.DiscoveredDevices.Count > 0);

        // Assert
        await Assert.That(laptop.DiscoveredDevices.Select(p => p.DeviceName)).IsEquivalentTo(["Desktop"]);
    }

    [Test]
    public async Task DeviceThatStopsAnnouncing_DropsOffAfterExpiryTime()
    {
        // Arrange
        var (laptop, laptopIdentity) = CreateDevice("Laptop", _laptopAddress);
        var (desktop, desktopIdentity) = CreateDevice("Desktop", _desktopAddress);
        await laptop.StartAsync(laptopIdentity, 47823);
        await desktop.StartAsync(desktopIdentity, 47823);
        await WaitUntilAsync(() => laptop.DiscoveredDevices.Count == 1);
        await desktop.StopAsync();
        await WaitForTimersAsync(1); // the laptop's announcement and sweep timers

        var changed = 0;
        laptop.DiscoveredDevicesChanged += (_, _) => Interlocked.Increment(ref changed);

        // Act: no time passes on its own, so the desktop sent nothing after its first announcement
        _clock.Advance(TestOptions.ExpiryTime + TimeSpan.FromSeconds(1));

        // Assert
        await Assert.That(laptop.DiscoveredDevices).IsEmpty();
        await WaitUntilAsync(() => Volatile.Read(ref changed) > 0); // the periodic sweep tells the UI
    }

    [Test]
    public async Task DeviceStillAnnouncing_DoesNotDropOff()
    {
        // Arrange
        var (laptop, laptopIdentity) = CreateDevice("Laptop", _laptopAddress);
        var (desktop, desktopIdentity) = CreateDevice("Desktop", _desktopAddress);
        await laptop.StartAsync(laptopIdentity, 47823);
        await desktop.StartAsync(desktopIdentity, 47823);
        await WaitUntilAsync(() => laptop.DiscoveredDevices.Count == 1);
        await WaitForTimersAsync(2);

        // Act: time passes, the desktop announces again, and more time passes than since its first announcement
        _clock.Advance(TestOptions.ExpiryTime - TimeSpan.FromSeconds(1));
        var announcedAt = _clock.GetUtcNow();
        await WaitUntilAsync(() => laptop.DiscoveredDevices.SingleOrDefault()?.LastSeen == announcedAt);
        _clock.Advance(TimeSpan.FromSeconds(2));

        // Assert
        await Assert.That(laptop.DiscoveredDevices.Count).IsEqualTo(1);
    }

    [Test]
    public async Task StopAsync_StopsSendingAndListeningAndClearsDiscoveredDevices()
    {
        // Arrange
        var (laptop, laptopIdentity) = CreateDevice("Laptop", _laptopAddress);
        var (desktop, desktopIdentity) = CreateDevice("Desktop", _desktopAddress);
        await laptop.StartAsync(laptopIdentity, 47823);
        await desktop.StartAsync(desktopIdentity, 47823);
        await WaitUntilAsync(() => laptop.DiscoveredDevices.Count == 1);

        // Act
        await laptop.StopAsync();
        var sentAfterStop = _network.Sent.Count(p => p.Source.Address.Equals(_laptopAddress));
        _clock.Advance(TestOptions.AnnouncementInterval * 4);

        // Assert
        await Assert.That(laptop.IsRunning).IsFalse();
        await Assert.That(laptop.DiscoveredDevices).IsEmpty();
        await Assert.That(_network.OpenTransports).IsEqualTo(1); // only the desktop's
        await Assert.That(_network.Sent.Count(p => p.Source.Address.Equals(_laptopAddress))).IsEqualTo(sentAfterStop);
    }

    [Test]
    public async Task StartAsync_UsesConfiguredDiscoveryPort()
    {
        // Arrange
        var factory = A.Fake<IDiscoveryTransportFactory>();
        A.CallTo(() => factory.Create(A<int>._)).Returns(_network.CreateFactory(_laptopAddress).Create(47824));
        var service = new DeviceDiscoveryService(factory, CreateConfigurationService("Laptop"), NullLogger<DeviceDiscoveryService>.Instance,
            new DeviceDiscoveryOptions { Port = 49999 }, _clock);
        _services.Add(service);

        // Act
        await service.StartAsync(CreateIdentity(), 47823);

        // Assert
        A.CallTo(() => factory.Create(49999)).MustHaveHappenedOnceExactly();
    }

    // The real intervals: every timer runs on the test's clock, which only moves when a test advances it
    private static DeviceDiscoveryOptions TestOptions { get; } = new();

    private (DeviceDiscoveryService Service, DeviceIdentity Identity) CreateDevice(string deviceName, IPAddress address)
    {
        var service = new DeviceDiscoveryService(_network.CreateFactory(address), CreateConfigurationService(deviceName),
            NullLogger<DeviceDiscoveryService>.Instance, TestOptions, _clock);
        _services.Add(service);
        return (service, CreateIdentity());
    }

    private static IConfigurationService CreateConfigurationService(string deviceName)
    {
        var configuration = new ClipMateConfiguration();
        configuration.Networking.DeviceName = deviceName;
        var configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => configurationService.Configuration).Returns(configuration);
        return configurationService;
    }

    private DeviceIdentity CreateIdentity()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=test", key, HashAlgorithmName.SHA256);
        var identity = new DeviceIdentity(request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1)));
        _identities.Add(identity);
        return identity;
    }

    /// <summary>
    /// Waits until the announcement and sweep loops of <paramref name="runningDevices" /> started Devices are all waiting
    /// on the test's clock, so advancing it fires them.
    /// </summary>
    private async Task WaitForTimersAsync(int runningDevices) => await WaitUntilAsync(() => _clock.PendingTimers >= runningDevices * 2);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(_timeout);
        while (!condition())
            await Task.Delay(10, timeout.Token);
    }
}
