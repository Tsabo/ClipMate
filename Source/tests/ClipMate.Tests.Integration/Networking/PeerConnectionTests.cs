using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data.Services;
using ClipMate.Networking;
using FakeItEasy;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClipMate.Tests.Integration.Networking;

/// <summary>
/// How every caller reaches a Peer. Here rather than in the unit tests because failed calls load gRPC, which the unit
/// test process must not (<c>StartAsync_NetworkingOff_DoesNotLoadAspNetCore</c>).
/// </summary>
public class PeerConnectionTests
{
    private const string _announced = "10.0.0.5:47823";
    private const string _remembered = "192.168.1.20:47823";
    private static readonly DeviceId _laptop = DeviceId.FromCertificateBytes([1]);
    private X509Certificate2 _certificate = null!;
    private PeerConnector _connector = null!;
    private IDeviceDiscoveryService _discovery = null!;
    private DeviceIdentity _identity = null!;
    private ILocalDevice _localDevice = null!;
    private PeerStore _store = null!;

    [Before(Test)]
    public async Task Setup()
    {
        var configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => configurationService.Configuration).Returns(new ClipMateConfiguration());
        _store = new PeerStore(configurationService, NullLogger<PeerStore>.Instance);
        await _store.SavePairedAsync(_laptop, "LAPTOP", null, _remembered);

        _discovery = A.Fake<IDeviceDiscoveryService>();
        A.CallTo(() => _discovery.DiscoveredDevices).Returns(
            [new DiscoveredDevice(_laptop, "LAPTOP", IPEndPoint.Parse(_announced), PeerProtocol.Major, 0, DateTimeOffset.UtcNow)]);

        _certificate = CreateCertificate();
        _identity = new DeviceIdentity(_certificate);
        _localDevice = A.Fake<ILocalDevice>();
        A.CallTo(() => _localDevice.Identity).Returns(_identity);
        _connector = new PeerConnector(_localDevice, _store, _discovery, NullLogger<PeerConnector>.Instance);
    }

    [After(Test)]
    public void Cleanup()
    {
        _identity.Dispose();
        _certificate.Dispose();
    }

    [Test]
    public async Task ForPeer_PairedDevice_TriesTheAnnouncedAddressBeforeTheRememberedOnes()
    {
        // Act
        var connection = _connector.ForPeer(_laptop);

        // Assert
        await Assert.That(connection).IsNotNull();
        await Assert.That(connection!.Identity).IsSameReferenceAs(_identity);
        await Assert.That(connection.Peer.DeviceId).IsEqualTo(_laptop);
        await Assert.That(string.Join(" ", connection.Addresses)).IsEqualTo($"{_announced} {_remembered}");
    }

    [Test]
    public async Task ForPeer_DeviceThatIsNotAPeer_IsNull()
    {
        // Act / Assert
        await Assert.That(_connector.ForPeer(DeviceId.FromCertificateBytes([2]))).IsNull();
    }

    [Test]
    public async Task ForPeer_NetworkingOff_IsNull()
    {
        // Arrange
        A.CallTo(() => _localDevice.Identity).Returns(null);

        // Act / Assert
        await Assert.That(_connector.ForPeer(_laptop)).IsNull();
    }

    [Test]
    [Arguments(null)]
    [Arguments("timeout")]
    [Arguments("format")]
    [Arguments("unavailable")]
    public async Task TryEachAddressAsync_NoAnswerAtTheFirstAddress_TriesTheNextAndReportsWhereItAnswered(string? failure)
    {
        // Arrange
        var connection = _connector.ForPeer(_laptop)!;
        var tried = new List<string>();

        // Act
        var answer = await connection.TryEachAddressAsync<string>((address, _) =>
        {
            tried.Add(address);
            if (address != _announced)
                return Task.FromResult<string?>("hello");

            return failure switch
            {
                "timeout" => throw new TimeoutException(),
                "format" => throw new FormatException(),
                "unavailable" => throw new RpcException(new Status(StatusCode.Unavailable, "No connection.")),
                var _ => Task.FromResult<string?>(null),
            };
        }, CancellationToken.None);

        // Assert
        await Assert.That(answer).IsEqualTo(new PeerAnswer<string>("hello", _remembered));
        await Assert.That(string.Join(" ", tried)).IsEqualTo($"{_announced} {_remembered}");
    }

    [Test]
    public async Task TryEachAddressAsync_PeerAnswersWithAnError_ThrowsItWithoutTryingTheOtherAddresses()
    {
        // Arrange
        var connection = _connector.ForPeer(_laptop)!;
        var tried = new List<string>();

        // Act / Assert
        await Assert.That(async () => await connection.TryEachAddressAsync<string>((address, _) =>
        {
            tried.Add(address);
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Not a Peer."));
        }, CancellationToken.None)).Throws<RpcException>();
        await Assert.That(string.Join(" ", tried)).IsEqualTo(_announced);
    }

    [Test]
    public async Task TryEachAddressAsync_NoAnswerAnywhere_IsNull()
    {
        // Arrange
        var connection = _connector.ForPeer(_laptop)!;

        // Act
        var answer = await connection.TryEachAddressAsync<string>((_, _) => Task.FromResult<string?>(null), CancellationToken.None);

        // Assert
        await Assert.That(answer).IsNull();
    }

    [Test]
    public async Task TryEachAddressAsync_Cancelled_StopsBeforeTheNextAddress()
    {
        // Arrange
        var connection = _connector.ForPeer(_laptop)!;
        using var cancellation = new CancellationTokenSource();
        var tried = new List<string>();

        // Act / Assert
        await Assert.That(async () => await connection.TryEachAddressAsync<string>((address, _) =>
        {
            tried.Add(address);
            cancellation.Cancel();
            return Task.FromResult<string?>(null);
        }, cancellation.Token)).Throws<OperationCanceledException>();
        await Assert.That(string.Join(" ", tried)).IsEqualTo(_announced);
    }

    [Test]
    public async Task TryEachAddressAsync_CallCancelled_IsReportedAsCancellation()
    {
        // Arrange: how Grpc.Net.Client reports a call whose token was cancelled
        var connection = _connector.ForPeer(_laptop)!;
        using var cancellation = new CancellationTokenSource();

        // Act / Assert
        await Assert.That(async () => await connection.TryEachAddressAsync<string>((_, _) =>
        {
            cancellation.Cancel();
            throw new RpcException(new Status(StatusCode.Cancelled, "Call canceled by the client."));
        }, cancellation.Token)).Throws<OperationCanceledException>();
    }

    private static X509Certificate2 CreateCertificate()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=Test", key, HashAlgorithmName.SHA256);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }
}
