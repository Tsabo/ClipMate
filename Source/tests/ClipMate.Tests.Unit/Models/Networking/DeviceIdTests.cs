using System.Security.Cryptography;
using ClipMate.Core.Models.Networking;

namespace ClipMate.Tests.Unit.Models.Networking;

public class DeviceIdTests
{
    // SHA-256 of the empty input, base32 (RFC 4648) without padding.
    private const string _emptyInputDeviceId = "4OYMIQUY7QOBJGX36TEJS35ZEQT24QPEMSNZGTFESWMRW6CSXBKQ";

    [Test]
    public async Task FromCertificateBytes_UsesBase32OfSha256()
    {
        // Act
        var deviceId = DeviceId.FromCertificateBytes([]);

        // Assert
        await Assert.That(deviceId.Value).IsEqualTo(_emptyInputDeviceId);
    }

    [Test]
    public async Task ToDisplayString_GroupsInFours()
    {
        // Arrange
        var deviceId = DeviceId.FromCertificateBytes([]);

        // Act
        var display = deviceId.ToDisplayString();

        // Assert
        await Assert.That(display).IsEqualTo("4OYM-IQUY-7QOB-JGX3-6TEJ-S35Z-EQT2-4QPE-MSNZ-GTFE-SWMR-W6CS-XBKQ");
    }

    [Test]
    public async Task TryParse_AcceptsDisplayFormInAnyCase()
    {
        // Act
        var parsed = DeviceId.TryParse(" 4oym-iquy-7QOB-JGX3-6TEJ-S35Z-EQT2-4QPE-MSNZ-GTFE-SWMR-W6CS-XBKQ ", out var deviceId);

        // Assert
        await Assert.That(parsed).IsTrue();
        await Assert.That(deviceId).IsEqualTo(DeviceId.FromCertificateBytes([]));
    }

    [Test]
    [Arguments("")]
    [Arguments("ABCD")]
    [Arguments("1OYMIQUY7QOBJGX36TEJS35ZEQT24QPEMSNZGTFESWMRW6CSXBKQ")]
    public async Task TryParse_InvalidText_ReturnsFalse(string text)
    {
        // Act
        var parsed = DeviceId.TryParse(text, out _);

        // Assert
        await Assert.That(parsed).IsFalse();
    }

    [Test]
    public async Task FromCertificate_DiffersForDifferentCertificates()
    {
        // Arrange
        using var first = CreateCertificate();
        using var second = CreateCertificate();

        // Act / Assert
        await Assert.That(DeviceId.FromCertificate(first)).IsNotEqualTo(DeviceId.FromCertificate(second));
        await Assert.That(DeviceId.FromCertificate(first)).IsEqualTo(DeviceId.FromCertificate(first));
    }

    private static System.Security.Cryptography.X509Certificates.X509Certificate2 CreateCertificate()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=test", key, HashAlgorithmName.SHA256);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }
}
