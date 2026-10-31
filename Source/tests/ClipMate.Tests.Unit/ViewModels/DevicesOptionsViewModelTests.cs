using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ClipMate.App.Services;
using ClipMate.App.ViewModels;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services.Abstractions;
using FakeItEasy;
using Microsoft.Extensions.Logging;

namespace ClipMate.Tests.Unit.ViewModels;

public partial class DevicesOptionsViewModelTests
{
    private ClipMateConfiguration _configuration = null!;
    private IConfigurationService _configurationService = null!;
    private IDeviceDiscoveryService _discovery = null!;
    private DeviceIdentity _identity = null!;
    private IDeviceIdentityService _identityService = null!;
    private INetworkProfileService _networkProfiles = null!;
    private IPairingDialogService _pairingDialogs = null!;
    private IPeerDialogService _peerDialogs = null!;
    private IPeerStatusService _peerStatus = null!;
    private IPeerStore _peerStore = null!;
    private IUnpairingService _unpairing = null!;
    private DevicesOptionsViewModel _viewModel = null!;

    [Before(Test)]
    public void Setup()
    {
        _configuration = new ClipMateConfiguration();
        _configurationService = A.Fake<IConfigurationService>();
        var configurationService = _configurationService;
        A.CallTo(() => configurationService.Configuration).Returns(_configuration);

        _identity = new DeviceIdentity(CreateCertificate());
        _identityService = A.Fake<IDeviceIdentityService>();
        A.CallTo(() => _identityService.TryLoadAsync(A<CancellationToken>._)).Returns((DeviceIdentity?)null);
        A.CallTo(() => _identityService.GetOrCreateAsync(A<CancellationToken>._)).ReturnsLazily(() => new DeviceIdentity(CreateCertificateCopy()));

        _discovery = A.Fake<IDeviceDiscoveryService>();
        A.CallTo(() => _discovery.DiscoveredDevices).Returns([]);

        _peerStore = A.Fake<IPeerStore>();
        A.CallTo(() => _peerStore.GetPeers()).Returns([]);
        _peerStatus = A.Fake<IPeerStatusService>();
        _pairingDialogs = A.Fake<IPairingDialogService>();
        _peerDialogs = A.Fake<IPeerDialogService>();
        _unpairing = A.Fake<IUnpairingService>();
        _networkProfiles = A.Fake<INetworkProfileService>();
        A.CallTo(() => _networkProfiles.GetPublicNetworkNames()).Returns([]);

        _viewModel = new DevicesOptionsViewModel(configurationService, _identityService, _discovery, _peerStore, _peerStatus, _pairingDialogs,
            _peerDialogs, _unpairing, _networkProfiles, A.Fake<ILogger<DevicesOptionsViewModel>>());
    }

    [After(Test)]
    public void Cleanup() => _identity.Dispose();

    [Test]
    public async Task LoadAsync_ReadsNetworkingSettings()
    {
        // Arrange
        _configuration.Networking.Enabled = true;
        _configuration.Networking.DeviceName = "Laptop";
        _configuration.Networking.Port = 50123;

        // Act
        await _viewModel.LoadAsync();

        // Assert
        await Assert.That(_viewModel.EnableNetworking).IsTrue();
        await Assert.That(_viewModel.DeviceName).IsEqualTo("Laptop");
        await Assert.That(_viewModel.Port).IsEqualTo(50123);
    }

    [Test]
    public async Task LoadAsync_NoIdentityYet_ShowsNoDeviceIdAndCreatesNone()
    {
        // Act
        await _viewModel.LoadAsync();

        // Assert
        await Assert.That(_viewModel.DeviceId).IsNull();
        await Assert.That(_viewModel.HasDeviceId).IsFalse();
        A.CallTo(() => _identityService.GetOrCreateAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task LoadAsync_IdentityExists_ShowsGroupedDeviceId()
    {
        // Arrange
        A.CallTo(() => _identityService.TryLoadAsync(A<CancellationToken>._)).ReturnsLazily(() => new DeviceIdentity(CreateCertificateCopy()));

        // Act
        await _viewModel.LoadAsync();

        // Assert
        await Assert.That(_viewModel.DeviceId).IsEqualTo(_identity.DeviceId.ToDisplayString());
        await Assert.That(_viewModel.DeviceIdQrText).IsEqualTo(_identity.DeviceId.Value);
        await Assert.That(_viewModel.HasDeviceId).IsTrue();
    }

    [Test]
    public async Task EnableNetworking_TurnedOn_GeneratesIdentityAndShowsDeviceId()
    {
        // Arrange
        await _viewModel.LoadAsync();

        // Act
        _viewModel.EnableNetworking = true;
        await _viewModel.EnsureDeviceIdAsync();

        // Assert
        await Assert.That(_viewModel.DeviceId).IsEqualTo(_identity.DeviceId.ToDisplayString());
        A.CallTo(() => _identityService.GetOrCreateAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task Save_WritesNetworkingSettings()
    {
        // Arrange
        await _viewModel.LoadAsync();
        _viewModel.DeviceName = "  Office PC  ";
        _viewModel.Port = 50200;
        _viewModel.EnableNetworking = true;

        // Act
        _viewModel.Save();

        // Assert
        await Assert.That(_configuration.Networking.Enabled).IsTrue();
        await Assert.That(_configuration.Networking.DeviceName).IsEqualTo("Office PC");
        await Assert.That(_configuration.Networking.Port).IsEqualTo(50200);
    }

    [Test]
    public async Task Save_BlankDeviceName_FallsBackToComputerName()
    {
        // Arrange
        await _viewModel.LoadAsync();
        _viewModel.DeviceName = "   ";

        // Act
        _viewModel.Save();

        // Assert
        await Assert.That(_configuration.Networking.DeviceName).IsEqualTo(Environment.MachineName);
    }

    [Test]
    public async Task StartWatching_ListsDeviceNameAndShortDeviceId()
    {
        // Arrange
        var desktop = CreateDiscoveredDevice("Desktop");
        A.CallTo(() => _discovery.DiscoveredDevices).Returns([desktop]);

        // Act
        _viewModel.StartWatching();

        // Assert
        var item = _viewModel.DiscoveredDevices.Single();
        await Assert.That(item.DeviceName).IsEqualTo("Desktop");
        await Assert.That(item.ShortDeviceId).IsEqualTo(desktop.DeviceId.ToShortDisplayString());
        await Assert.That(item.DeviceId).IsEqualTo(desktop.DeviceId.ToDisplayString());
    }

    [Test]
    public async Task DiscoveredDevicesChanged_WhileWatching_UpdatesTheList()
    {
        // Arrange
        A.CallTo(() => _discovery.DiscoveredDevices).Returns([CreateDiscoveredDevice("Desktop")]);
        _viewModel.StartWatching();

        // Act: the desktop stops announcing and drops off
        A.CallTo(() => _discovery.DiscoveredDevices).Returns([]);
        _discovery.DiscoveredDevicesChanged += Raise.WithEmpty();

        // Assert
        await Assert.That(_viewModel.DiscoveredDevices).IsEmpty();
    }

    [Test]
    public async Task StopWatching_IgnoresLaterChanges()
    {
        // Arrange
        _viewModel.StartWatching();

        // Act
        _viewModel.StopWatching();
        A.CallTo(() => _discovery.DiscoveredDevices).Returns([CreateDiscoveredDevice("Desktop")]);
        _discovery.DiscoveredDevicesChanged += Raise.WithEmpty();

        // Assert
        await Assert.That(_viewModel.DiscoveredDevices).IsEmpty();
    }

    [Test]
    [Arguments(false, "Turn on networking to find your other Devices on this network.")]
    [Arguments(true, "Looking for your other Devices on this network...")]
    public async Task StartWatching_NoneFound_ExplainsWhy(bool discovering, string expected)
    {
        // Arrange
        A.CallTo(() => _discovery.IsRunning).Returns(discovering);

        // Act
        _viewModel.StartWatching();

        // Assert
        await Assert.That(_viewModel.HasDiscoveredDevices).IsFalse();
        await Assert.That(_viewModel.DiscoveryStatusText).IsEqualTo(expected);
    }

    private static DiscoveredDevice CreateDiscoveredDevice(string deviceName)
    {
        using var certificate = CreateCertificate();
        return new DiscoveredDevice(DeviceId.FromCertificate(certificate), deviceName, new IPEndPoint(IPAddress.Parse("192.168.1.20"), 47823),
            ClipMate.Networking.PeerProtocol.Major, 0, DateTimeOffset.UtcNow);
    }

    private static X509Certificate2 CreateCertificate()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=test", key, HashAlgorithmName.SHA256);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }

    private X509Certificate2 CreateCertificateCopy() =>
        X509CertificateLoader.LoadPkcs12(_identity.Certificate.Export(X509ContentType.Pkcs12), null);
}
