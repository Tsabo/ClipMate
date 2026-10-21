using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using ClipMate.Core.Models.Networking;
using Grpc.Net.Client;

namespace ClipMate.Networking;

/// <summary>
/// Creates gRPC channels to a Peer over mutual TLS: this Device presents its own certificate and
/// accepts the server only if its certificate matches the expected Device ID.
/// </summary>
public static class PeerChannelFactory
{
    /// <summary>
    /// Creates a channel to <paramref name="address" /> that only talks to the Device with <paramref name="expectedPeer" />.
    /// </summary>
    public static GrpcChannel Create(Uri address, DeviceIdentity identity, DeviceId expectedPeer)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(identity);

        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(10),
            SslOptions = new SslClientAuthenticationOptions
            {
                ClientCertificates = new X509CertificateCollection { identity.Certificate },
                LocalCertificateSelectionCallback = (_, _, _, _, _) => identity.Certificate,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                RemoteCertificateValidationCallback = (_, certificate, _, _) =>
                    certificate != null && DeviceId.FromCertificateBytes(certificate.GetRawCertData()) == expectedPeer,
            },
        };

        return GrpcChannel.ForAddress(address, new GrpcChannelOptions { HttpHandler = handler, DisposeHttpClient = true });
    }
}
