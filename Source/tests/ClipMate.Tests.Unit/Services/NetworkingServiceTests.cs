using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ClipMate.App.Services;
using ClipMate.Core.Events;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.Extensions.Logging;

namespace ClipMate.Tests.Unit.Services;

public class NetworkingServiceTests
{
    private ClipMateConfiguration _configuration = null!;
    private IDeviceDiscoveryService _discovery = null!;
    private IFirewallRuleService _firewall = null!;
    private IPeerHost _host = null!;
    private IPeerHostFactory _hostFactory = null!;
    private IDeviceIdentityService _identityService = null!;
    private IMessenger _messenger = null!;
    private IPeerStatusService _peerStatus = null!;
    private LocalDevice _localDevice = null!;
    private NetworkingService _service = null!;

    [Before(Test)]
    public void Setup()
    {
        _configuration = new ClipMateConfiguration();
        var configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => configurationService.Configuration).Returns(_configuration);

        _identityService = A.Fake<IDeviceIdentityService>();
        A.CallTo(() => _identityService.GetOrCreateAsync(A<CancellationToken>._)).ReturnsLazily(() => new DeviceIdentity(CreateCertificate()));

        _firewall = A.Fake<IFirewallRuleService>();
        A.CallTo(() => _firewall.EnsureInboundRuleAsync(A<CancellationToken>._)).Returns(true);

        _host = A.Fake<IPeerHost>();
        _hostFactory = A.Fake<IPeerHostFactory>();
        A.CallTo(() => _hostFactory.Create()).Returns(_host);

        _discovery = A.Fake<IDeviceDiscoveryService>();
        _peerStatus = A.Fake<IPeerStatusService>();
        _messenger = new StrongReferenceMessenger();
        _localDevice = new LocalDevice(configurationService);
        _service = new NetworkingService(configurationService, _identityService, _firewall, _hostFactory, _discovery, _peerStatus, _localDevice, _messenger,
            A.Fake<ILogger<NetworkingService>>());
    }

    [After(Test)]
    public void Cleanup() => _service.Dispose();

    [Test]
    public async Task ApplyConfigurationAsync_NetworkingOff_TouchesNoNetworkingComponent()
    {
        // Act
        await _service.ApplyConfigurationAsync();

        // Assert
        await Assert.That(_service.IsListening).IsFalse();
        A.CallTo(() => _hostFactory.Create()).MustNotHaveHappened();
        A.CallTo(() => _identityService.GetOrCreateAsync(A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => _firewall.EnsureInboundRuleAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task ApplyConfigurationAsync_NetworkingOn_StartsHostOnConfiguredPortAndAddsFirewallRule()
    {
        // Arrange
        _configuration.Networking.Enabled = true;
        _configuration.Networking.Port = 50123;

        // Act
        await _service.ApplyConfigurationAsync();

        // Assert
        await Assert.That(_service.IsListening).IsTrue();
        A.CallTo(() => _identityService.GetOrCreateAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _firewall.EnsureInboundRuleAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _host.StartAsync(A<DeviceIdentity>._, 50123, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task ApplyConfigurationAsync_TurnedOff_StopsHost()
    {
        // Arrange
        _configuration.Networking.Enabled = true;
        await _service.ApplyConfigurationAsync();

        // Act
        _configuration.Networking.Enabled = false;
        await _service.ApplyConfigurationAsync();

        // Assert
        await Assert.That(_service.IsListening).IsFalse();
        A.CallTo(() => _host.DisposeAsync()).MustHaveHappened();
    }

    [Test]
    public async Task ApplyConfigurationAsync_UnchangedWhileRunning_DoesNotRestart()
    {
        // Arrange
        _configuration.Networking.Enabled = true;
        await _service.ApplyConfigurationAsync();

        // Act
        await _service.ApplyConfigurationAsync();

        // Assert
        A.CallTo(() => _hostFactory.Create()).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task ApplyConfigurationAsync_PortChangedWhileRunning_RestartsOnNewPort()
    {
        // Arrange
        _configuration.Networking.Enabled = true;
        _configuration.Networking.Port = 50123;
        await _service.ApplyConfigurationAsync();

        // Act
        _configuration.Networking.Port = 50124;
        await _service.ApplyConfigurationAsync();

        // Assert
        A.CallTo(() => _host.DisposeAsync()).MustHaveHappenedOnceExactly();
        A.CallTo(() => _host.StartAsync(A<DeviceIdentity>._, 50124, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task ApplyConfigurationAsync_HostFailsToStart_ReportsErrorAndStaysOff()
    {
        // Arrange
        _configuration.Networking.Enabled = true;
        A.CallTo(() => _host.StartAsync(A<DeviceIdentity>._, A<int>._, A<CancellationToken>._))
            .ThrowsAsync(new IOException("Address already in use"));

        var statusUpdates = new List<StatusUpdateEvent>();
        _messenger.Register<StatusUpdateEvent>(this, (_, message) => statusUpdates.Add(message));

        // Act
        await _service.ApplyConfigurationAsync();

        // Assert
        await Assert.That(_service.IsListening).IsFalse();
        A.CallTo(() => _host.DisposeAsync()).MustHaveHappened();
        await Assert.That(statusUpdates.Any(p => p.IsError)).IsTrue();
    }

    [Test]
    public async Task ApplyConfigurationAsync_NetworkingOn_StartsDiscoveryWithPeerListenerPort()
    {
        // Arrange
        _configuration.Networking.Enabled = true;
        _configuration.Networking.Port = 50123;

        // Act
        await _service.ApplyConfigurationAsync();

        // Assert
        A.CallTo(() => _discovery.StartAsync(A<DeviceIdentity>._, 50123, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task ApplyConfigurationAsync_NetworkingOff_DoesNotStartDiscovery()
    {
        // Act
        await _service.ApplyConfigurationAsync();

        // Assert
        A.CallTo(() => _discovery.StartAsync(A<DeviceIdentity>._, A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task ApplyConfigurationAsync_TurnedOff_StopsDiscovery()
    {
        // Arrange
        _configuration.Networking.Enabled = true;
        await _service.ApplyConfigurationAsync();

        // Act
        _configuration.Networking.Enabled = false;
        await _service.ApplyConfigurationAsync();

        // Assert
        A.CallTo(() => _discovery.StopAsync(A<CancellationToken>._)).MustHaveHappened();
    }

    [Test]
    public async Task ApplyConfigurationAsync_TurnedOnThenOff_StartsAndStopsPeerStatusChecks()
    {
        // Arrange
        _configuration.Networking.Enabled = true;

        // Act / Assert
        await _service.ApplyConfigurationAsync();
        A.CallTo(() => _peerStatus.StartAsync(A<DeviceIdentity>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        await Assert.That(_localDevice.Identity).IsNotNull();

        _configuration.Networking.Enabled = false;
        await _service.ApplyConfigurationAsync();
        A.CallTo(() => _peerStatus.StopAsync(A<CancellationToken>._)).MustHaveHappened();
        await Assert.That(_localDevice.Identity).IsNull();
    }

    [Test]
    public async Task ApplyConfigurationAsync_DiscoveryFailsToStart_KeepsHostListeningAndReportsError()
    {
        // Arrange
        _configuration.Networking.Enabled = true;
        A.CallTo(() => _discovery.StartAsync(A<DeviceIdentity>._, A<int>._, A<CancellationToken>._))
            .ThrowsAsync(new IOException("Address already in use"));

        var statusUpdates = new List<StatusUpdateEvent>();
        _messenger.Register<StatusUpdateEvent>(this, (_, message) => statusUpdates.Add(message));

        // Act
        await _service.ApplyConfigurationAsync();

        // Assert
        await Assert.That(_service.IsListening).IsTrue();
        await Assert.That(statusUpdates.Any(p => p.IsError)).IsTrue();
    }

    [Test]
    public async Task ApplyConfigurationAsync_HostFailsToStart_DoesNotStartDiscovery()
    {
        // Arrange
        _configuration.Networking.Enabled = true;
        A.CallTo(() => _host.StartAsync(A<DeviceIdentity>._, A<int>._, A<CancellationToken>._))
            .ThrowsAsync(new IOException("Address already in use"));

        // Act
        await _service.ApplyConfigurationAsync();

        // Assert
        A.CallTo(() => _discovery.StartAsync(A<DeviceIdentity>._, A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task StopAsync_WhileRunning_StopsHost()
    {
        // Arrange
        _configuration.Networking.Enabled = true;
        await _service.ApplyConfigurationAsync();

        // Act
        await _service.StopAsync(CancellationToken.None);

        // Assert
        await Assert.That(_service.IsListening).IsFalse();
        A.CallTo(() => _host.DisposeAsync()).MustHaveHappened();
    }

    private static X509Certificate2 CreateCertificate()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=test", key, HashAlgorithmName.SHA256);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }
}
