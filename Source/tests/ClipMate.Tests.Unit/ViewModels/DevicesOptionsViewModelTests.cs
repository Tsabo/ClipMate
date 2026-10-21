using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ClipMate.App.ViewModels;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using FakeItEasy;
using Microsoft.Extensions.Logging;

namespace ClipMate.Tests.Unit.ViewModels;

public class DevicesOptionsViewModelTests
{
    private ClipMateConfiguration _configuration = null!;
    private DeviceIdentity _identity = null!;
    private IDeviceIdentityService _identityService = null!;
    private DevicesOptionsViewModel _viewModel = null!;

    [Before(Test)]
    public void Setup()
    {
        _configuration = new ClipMateConfiguration();
        var configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => configurationService.Configuration).Returns(_configuration);

        _identity = new DeviceIdentity(CreateCertificate());
        _identityService = A.Fake<IDeviceIdentityService>();
        A.CallTo(() => _identityService.TryLoadAsync(A<CancellationToken>._)).Returns((DeviceIdentity?)null);
        A.CallTo(() => _identityService.GetOrCreateAsync(A<CancellationToken>._)).ReturnsLazily(() => new DeviceIdentity(CreateCertificateCopy()));

        _viewModel = new DevicesOptionsViewModel(configurationService, _identityService, A.Fake<ILogger<DevicesOptionsViewModel>>());
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

    private static X509Certificate2 CreateCertificate()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=test", key, HashAlgorithmName.SHA256);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }

    private X509Certificate2 CreateCertificateCopy() =>
        X509CertificateLoader.LoadPkcs12(_identity.Certificate.Export(X509ContentType.Pkcs12), null);
}
