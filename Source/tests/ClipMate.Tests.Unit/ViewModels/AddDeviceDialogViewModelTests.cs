using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ClipMate.App.ViewModels;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using FakeItEasy;

namespace ClipMate.Tests.Unit.ViewModels;

public class AddDeviceDialogViewModelTests
{
    private static readonly DeviceId _desktop = DeviceId.FromCertificateBytes([2]);
    private DeviceIdentity _identity = null!;
    private ILocalDevice _localDevice = null!;
    private IPeerStore _peerStore = null!;
    private AddDeviceDialogViewModel _viewModel = null!;

    [Before(Test)]
    public void Setup()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=ClipMate Device", key, HashAlgorithmName.SHA256);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        _identity = new DeviceIdentity(X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx), null));

        _localDevice = A.Fake<ILocalDevice>();
        A.CallTo(() => _localDevice.Identity).Returns(_identity);
        _peerStore = A.Fake<IPeerStore>();
        _viewModel = new AddDeviceDialogViewModel(_localDevice, _peerStore);
    }

    [After(Test)]
    public void Cleanup() => _identity.Dispose();

    [Test]
    public async Task New_DefaultsToTheStandardPort()
    {
        // Assert
        await Assert.That(_viewModel.Port).IsEqualTo(47823);
        await Assert.That(_viewModel.Target).IsNull();
    }

    [Test]
    [Arguments("192.168.1.20", 47823, "192.168.1.20:47823")]
    [Arguments("  office-pc.example.com ", 50000, "office-pc.example.com:50000")]
    [Arguments("fe80::1", 47823, "[fe80::1]:47823")]
    [Arguments("[2001:db8::5]", 47823, "[2001:db8::5]:47823")]
    [Arguments("10.8.0.3:50123", 47823, "10.8.0.3:50123")]
    [Arguments("vpn-host:50123", 47823, "vpn-host:50123")]
    [Arguments("[2001:db8::5]:50123", 47823, "[2001:db8::5]:50123")]
    public async Task Pair_WithAnAddressAndDeviceId_CreatesTheTargetAndCloses(string address, int port, string expectedAddress)
    {
        // Arrange
        var closed = false;
        _viewModel.CloseRequested += (_, _) => closed = true;
        _viewModel.Address = address;
        _viewModel.Port = port;
        _viewModel.DeviceId = _desktop.ToDisplayString().ToLowerInvariant();

        // Act
        _viewModel.PairCommand.Execute(null);

        // Assert
        await Assert.That(_viewModel.ErrorText).IsNull();
        await Assert.That(_viewModel.Target).IsEqualTo(new PairingTarget(_desktop, string.Empty, expectedAddress));
        await Assert.That(closed).IsTrue();
    }

    [Test]
    [Arguments("", "Enter the other Device's host name or IP address.")]
    [Arguments("not a host!", "'not a host!' is not a valid host name or IP address.")]
    [Arguments("host:0", "Enter a port between 1 and 65535.")]
    public async Task Pair_WithABadAddress_ExplainsAndDoesNotClose(string address, string expectedError)
    {
        // Arrange
        var closed = false;
        _viewModel.CloseRequested += (_, _) => closed = true;
        _viewModel.Address = address;
        _viewModel.DeviceId = _desktop.Value;

        // Act
        _viewModel.PairCommand.Execute(null);

        // Assert
        await Assert.That(_viewModel.ErrorText).IsEqualTo(expectedError);
        await Assert.That(_viewModel.Target).IsNull();
        await Assert.That(closed).IsFalse();
    }

    [Test]
    public async Task Pair_WithAPortOutOfRange_Explains()
    {
        // Arrange
        _viewModel.Address = "192.168.1.20";
        _viewModel.Port = 70000;
        _viewModel.DeviceId = _desktop.Value;

        // Act
        _viewModel.PairCommand.Execute(null);

        // Assert
        await Assert.That(_viewModel.ErrorText).IsEqualTo("Enter a port between 1 and 65535.");
        await Assert.That(_viewModel.Target).IsNull();
    }

    [Test]
    public async Task Pair_WithAnAddressAlone_RequiresTheDeviceId()
    {
        // Arrange
        _viewModel.Address = "192.168.1.20";

        // Act
        _viewModel.PairCommand.Execute(null);

        // Assert
        await Assert.That(_viewModel.ErrorText).IsEqualTo(
            "Enter the other Device's Device ID as well; an address alone is not enough. It is shown on that Device's Devices page.");
        await Assert.That(_viewModel.Target).IsNull();
    }

    [Test]
    [Arguments("ABCD-EFGH")]
    [Arguments("0000000000000000000000000000000000000000000000000000")]
    public async Task Pair_WithAMalformedDeviceId_Explains(string deviceId)
    {
        // Arrange
        _viewModel.Address = "192.168.1.20";
        _viewModel.DeviceId = deviceId;

        // Act
        _viewModel.PairCommand.Execute(null);

        // Assert
        await Assert.That(_viewModel.ErrorText).IsEqualTo(
            "That is not a valid Device ID. A Device ID has 52 letters and digits (A-Z, 2-7), shown in groups of four.");
        await Assert.That(_viewModel.Target).IsNull();
    }

    [Test]
    public async Task Pair_WithThisDevicesOwnDeviceId_Explains()
    {
        // Arrange
        _viewModel.Address = "192.168.1.20";
        _viewModel.DeviceId = _identity.DeviceId.ToDisplayString();

        // Act
        _viewModel.PairCommand.Execute(null);

        // Assert
        await Assert.That(_viewModel.ErrorText).IsEqualTo(
            "That is this Device's own Device ID. Enter the Device ID of the Device you want to pair with.");
        await Assert.That(_viewModel.Target).IsNull();
    }

    [Test]
    public async Task Pair_WithAPeersDeviceId_SaysItIsAlreadyPaired()
    {
        // Arrange
        A.CallTo(() => _peerStore.IsPaired(_desktop)).Returns(true);
        _viewModel.Address = "192.168.1.20";
        _viewModel.DeviceId = _desktop.Value;

        // Act
        _viewModel.PairCommand.Execute(null);

        // Assert
        await Assert.That(_viewModel.ErrorText).IsEqualTo("You are already paired with that Device.");
        await Assert.That(_viewModel.Target).IsNull();
    }

    [Test]
    public async Task Pair_WhileNetworkingIsOff_AsksToTurnItOnFirst()
    {
        // Arrange
        A.CallTo(() => _localDevice.Identity).Returns(null);
        _viewModel.Address = "192.168.1.20";
        _viewModel.DeviceId = _desktop.Value;

        // Act
        _viewModel.PairCommand.Execute(null);

        // Assert
        await Assert.That(_viewModel.ErrorText).IsEqualTo("Turn on networking and click OK first, then add the Device.");
        await Assert.That(_viewModel.Target).IsNull();
    }

    [Test]
    public async Task Cancel_ClosesWithoutATarget()
    {
        // Arrange
        var closed = false;
        _viewModel.CloseRequested += (_, _) => closed = true;
        _viewModel.Address = "192.168.1.20";
        _viewModel.DeviceId = _desktop.Value;

        // Act
        _viewModel.CancelCommand.Execute(null);

        // Assert
        await Assert.That(closed).IsTrue();
        await Assert.That(_viewModel.Target).IsNull();
    }
}
