using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using ClipMate.Data.Services;
using ClipMate.Networking;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClipMate.Tests.Unit.Networking;

public class PeerStatusMonitorTests
{
    private static readonly DeviceId _laptop = DeviceId.FromCertificateBytes([1]);
    private IDeviceDiscoveryService _discovery = null!;
    private DeviceIdentity _identity = null!;
    private PeerStatusMonitor _monitor = null!;
    private StubProbe _probe = null!;
    private PeerStore _store = null!;

    [Before(Test)]
    public async Task Setup()
    {
        var configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => configurationService.Configuration).Returns(new ClipMateConfiguration());
        _store = new PeerStore(configurationService, NullLogger<PeerStore>.Instance);
        await _store.SavePairedAsync(_laptop, "LAPTOP", null, "192.168.1.20:47823");

        _discovery = A.Fake<IDeviceDiscoveryService>();
        A.CallTo(() => _discovery.DiscoveredDevices).Returns([]);
        _probe = new StubProbe();
        _identity = new DeviceIdentity(CreateCertificate());
        _monitor = new PeerStatusMonitor(_store, _discovery, _probe, NullLogger<PeerStatusMonitor>.Instance);
    }

    [After(Test)]
    public async Task Cleanup()
    {
        await _monitor.DisposeAsync();
        _identity.Dispose();
    }

    [Test]
    public async Task RefreshAsync_PeerAnswersWithTheSameMajorVersion_IsOnlineAndRecordsLastSeen()
    {
        // Arrange
        _probe.Replies["192.168.1.20:47823"] = new PeerProbeReply("LAPTOP (renamed)", PeerProtocol.Major, 7, ["pairing", "future"]);
        await _monitor.StartAsync(_identity);

        // Act
        await _monitor.RefreshAsync();

        // Assert
        await Assert.That(_monitor.GetStatus(_laptop)).IsEqualTo(PeerConnectionStatus.Online);
        var peer = _store.Find(_laptop)!;
        await Assert.That(peer.LastSeen).IsNotNull();
        await Assert.That(peer.DeviceName).IsEqualTo("LAPTOP (renamed)");
    }

    [Test]
    public async Task RefreshAsync_PeerAnswersWithAnotherMajorVersion_IsUpdateRequired()
    {
        // Arrange
        _probe.Replies["192.168.1.20:47823"] = new PeerProbeReply("LAPTOP", PeerProtocol.Major + 1, 0, []);
        await _monitor.StartAsync(_identity);

        // Act
        await _monitor.RefreshAsync();

        // Assert
        await Assert.That(_monitor.GetStatus(_laptop)).IsEqualTo(PeerConnectionStatus.UpdateRequired);
    }

    [Test]
    public async Task RefreshAsync_PeerDoesNotAnswer_IsOfflineAndKeepsLastSeen()
    {
        // Arrange
        await _monitor.StartAsync(_identity);

        // Act
        await _monitor.RefreshAsync();

        // Assert
        await Assert.That(_monitor.GetStatus(_laptop)).IsEqualTo(PeerConnectionStatus.Offline);
        await Assert.That(_store.Find(_laptop)!.LastSeen).IsNull();
    }

    [Test]
    public async Task RefreshAsync_PeerAnnouncesANewAddress_TriesItAndRemembersIt()
    {
        // Arrange
        A.CallTo(() => _discovery.DiscoveredDevices).Returns(
            [new DiscoveredDevice(_laptop, "LAPTOP", IPEndPoint.Parse("10.0.0.5:47823"), PeerProtocol.Major, 0, DateTimeOffset.UtcNow)]);
        _probe.Replies["10.0.0.5:47823"] = new PeerProbeReply("LAPTOP", PeerProtocol.Major, 0, []);
        await _monitor.StartAsync(_identity);

        // Act
        await _monitor.RefreshAsync();

        // Assert
        await Assert.That(_monitor.GetStatus(_laptop)).IsEqualTo(PeerConnectionStatus.Online);
        await Assert.That(_store.Find(_laptop)!.Addresses[0]).IsEqualTo("10.0.0.5:47823");
    }

    [Test]
    public async Task RefreshAsync_PeerSaysThisDeviceIsNotItsPeer_UnpairsItHere()
    {
        // Arrange
        _probe.NotPairedAt.Add("192.168.1.20:47823");
        var removed = new List<DeviceId>();
        _store.PeerRemoved += (_, e) => removed.Add(e.DeviceId);
        await _monitor.StartAsync(_identity);

        // Act
        await _monitor.RefreshAsync();

        // Assert
        await Assert.That(_store.IsPaired(_laptop)).IsFalse();
        await Assert.That(removed).IsEquivalentTo([_laptop]);
        await Assert.That(_monitor.GetStatus(_laptop)).IsEqualTo(PeerConnectionStatus.Unknown);
    }

    [Test]
    public async Task StopAsync_ForgetsStatuses()
    {
        // Arrange
        _probe.Replies["192.168.1.20:47823"] = new PeerProbeReply("LAPTOP", PeerProtocol.Major, 0, []);
        await _monitor.StartAsync(_identity);
        await _monitor.RefreshAsync();

        // Act
        await _monitor.StopAsync();

        // Assert
        await Assert.That(_monitor.GetStatus(_laptop)).IsEqualTo(PeerConnectionStatus.Unknown);
    }

    [Test]
    public async Task StatusChanged_IsRaisedWhenAPeerComesOnline()
    {
        // Arrange
        _probe.Replies["192.168.1.20:47823"] = new PeerProbeReply("LAPTOP", PeerProtocol.Major, 0, []);
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _monitor.StatusChanged += (_, _) => changed.TrySetResult();

        // Act
        await _monitor.StartAsync(_identity);
        await _monitor.RefreshAsync();

        // Assert
        await changed.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static X509Certificate2 CreateCertificate()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=Test", key, HashAlgorithmName.SHA256);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }

    private sealed class StubProbe : IPeerProbe
    {
        public Dictionary<string, PeerProbeReply> Replies { get; } = [];

        public HashSet<string> NotPairedAt { get; } = [];

        public Task<PeerProbeResult> ProbeAsync(DeviceIdentity identity, DeviceId peer, string address, CancellationToken cancellationToken) =>
            Task.FromResult(NotPairedAt.Contains(address) ? PeerProbeResult.NotPaired
                : Replies.TryGetValue(address, out var reply) ? PeerProbeResult.Reached(reply)
                : PeerProbeResult.Unreachable);
    }
}
