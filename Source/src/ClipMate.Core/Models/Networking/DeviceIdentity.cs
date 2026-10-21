using System.Security.Cryptography.X509Certificates;

namespace ClipMate.Core.Models.Networking;

/// <summary>
/// This Device's cryptographic identity: its self-signed certificate (with private key) and the Device ID derived from it.
/// </summary>
public sealed class DeviceIdentity : IDisposable
{
    /// <summary>
    /// Creates an identity from a certificate that carries its private key.
    /// </summary>
    public DeviceIdentity(X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        if (!certificate.HasPrivateKey)
            throw new ArgumentException("A Device certificate must include its private key.", nameof(certificate));

        Certificate = certificate;
        DeviceId = DeviceId.FromCertificate(certificate);
    }

    /// <summary>
    /// The Device certificate, including its private key.
    /// </summary>
    public X509Certificate2 Certificate { get; }

    /// <summary>
    /// The Device ID derived from <see cref="Certificate" />.
    /// </summary>
    public DeviceId DeviceId { get; }

    /// <inheritdoc />
    public void Dispose() => Certificate.Dispose();
}
