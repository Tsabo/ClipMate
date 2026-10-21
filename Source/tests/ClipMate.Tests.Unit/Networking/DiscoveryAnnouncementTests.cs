using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ClipMate.Core.Models.Networking;
using ClipMate.Networking;
using ClipMate.Networking.Discovery;

namespace ClipMate.Tests.Unit.Networking;

public class DiscoveryAnnouncementTests
{
    private DeviceIdentity _identity = null!;
    private DeviceIdentity _other = null!;

    [Before(Test)]
    public void Setup()
    {
        _identity = new DeviceIdentity(CreateCertificate("CN=laptop"));
        _other = new DeviceIdentity(CreateCertificate("CN=desktop"));
    }

    [After(Test)]
    public void Cleanup()
    {
        _identity.Dispose();
        _other.Dispose();
    }

    [Test]
    public async Task TryDecode_SignedAnnouncement_ReturnsDeviceIdNamePortAndProtocolVersion()
    {
        // Arrange
        var datagram = DiscoveryAnnouncement.Encode(_identity, "Laptop", 47823);

        // Act
        var decoded = DiscoveryAnnouncement.TryDecode(datagram, out var announcement);

        // Assert
        await Assert.That(decoded).IsTrue();
        await Assert.That(announcement!.DeviceId).IsEqualTo(_identity.DeviceId);
        await Assert.That(announcement.DeviceName).IsEqualTo("Laptop");
        await Assert.That(announcement.Port).IsEqualTo(47823);
        await Assert.That(announcement.ProtocolMajor).IsEqualTo(PeerProtocol.Major);
        await Assert.That(announcement.ProtocolMinor).IsEqualTo(PeerProtocol.Minor);
    }

    [Test]
    public async Task TryDecode_TamperedPayload_IsRejected()
    {
        // Arrange: change the Device Name inside the signed payload without re-signing
        var datagram = DiscoveryAnnouncement.Encode(_identity, "Laptop", 47823);
        var index = IndexOf(datagram, "Laptop"u8.ToArray());
        datagram[index] = (byte)'X';

        // Act
        var decoded = DiscoveryAnnouncement.TryDecode(datagram, out _);

        // Assert
        await Assert.That(decoded).IsFalse();
    }

    [Test]
    public async Task TryDecode_SignatureFromAnotherDeviceKey_IsRejected()
    {
        // Arrange: claim this Device's certificate (and so its Device ID) but sign with another Device's key
        var forged = DiscoveryAnnouncement.Encode(_identity, "Laptop", 47823, _other.Certificate.GetECDsaPrivateKey()!);

        // Act
        var decoded = DiscoveryAnnouncement.TryDecode(forged, out _);

        // Assert
        await Assert.That(decoded).IsFalse();
    }

    [Test]
    public async Task TryDecode_ClaimedDeviceIdNotMatchingCertificate_IsRejected()
    {
        // Arrange
        var forged = DiscoveryAnnouncement.Encode(_identity, "Laptop", 47823, claimedDeviceId: _other.DeviceId);

        // Act
        var decoded = DiscoveryAnnouncement.TryDecode(forged, out _);

        // Assert
        await Assert.That(decoded).IsFalse();
    }

    [Test]
    [Arguments(new byte[0])]
    [Arguments(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 })]
    public async Task TryDecode_Garbage_IsRejected(byte[] datagram)
    {
        // Act
        var decoded = DiscoveryAnnouncement.TryDecode(datagram, out _);

        // Assert
        await Assert.That(decoded).IsFalse();
    }

    [Test]
    public async Task TryDecode_UnknownFormatVersion_IsRejected()
    {
        // Arrange: the byte after the 4-byte magic is the format version
        var datagram = DiscoveryAnnouncement.Encode(_identity, "Laptop", 47823);
        datagram[4] = 99;

        // Act
        var decoded = DiscoveryAnnouncement.TryDecode(datagram, out _);

        // Assert
        await Assert.That(decoded).IsFalse();
    }

    private static int IndexOf(byte[] haystack, byte[] needle) => haystack.AsSpan().IndexOf(needle);

    private static X509Certificate2 CreateCertificate(string subject)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
    }
}
