using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using ClipMate.Core.Models.Networking;
using Grpc.Net.Client;

namespace ClipMate.Networking;

/// <summary>
/// Creates gRPC channels to another Device over mutual TLS: this Device presents its own certificate and
/// accepts the server only if its certificate matches the expected Device ID.
/// </summary>
public static class PeerChannelFactory
{
    /// <summary>
    /// Creates a channel to <paramref name="address" /> that only talks to the Device with <paramref name="expectedPeer" />.
    /// The TLS server name is the address's host, so the connection is for Peers only.
    /// </summary>
    public static GrpcChannel Create(Uri address, DeviceIdentity identity, DeviceId expectedPeer)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(identity);

        return GrpcChannel.ForAddress(address, new GrpcChannelOptions
        {
            HttpHandler = CreateHandler(identity, expectedPeer, null),
            DisposeHttpClient = true,
        });
    }

    /// <summary>
    /// Creates a channel to the Peer at <paramref name="address" /> (<c>host:port</c>). The other Device refuses the
    /// connection during the TLS handshake unless this Device is its Peer.
    /// </summary>
    public static GrpcChannel CreateForPeer(string address, DeviceIdentity identity, DeviceId expectedPeer) =>
        Create(address, PeerTlsServerNames.Peer, identity, expectedPeer);

    /// <summary>
    /// Creates a channel for Pairing with the Device at <paramref name="address" /> (<c>host:port</c>).
    /// The other Device accepts the connection from an unpaired Device, but serves it nothing except the pairing calls.
    /// </summary>
    public static GrpcChannel CreateForPairing(string address, DeviceIdentity identity, DeviceId expectedPeer) =>
        Create(address, PeerTlsServerNames.Pairing, identity, expectedPeer);

    private static GrpcChannel Create(string address, string serverName, DeviceIdentity identity, DeviceId expectedPeer)
    {
        ArgumentNullException.ThrowIfNull(identity);
        var endPoint = PeerAddress.Parse(address);

        // The URI host is only the TLS server name (SNI); the connection goes to the real address.
        return GrpcChannel.ForAddress(new Uri($"https://{serverName}:{PeerAddress.GetPort(endPoint)}"), new GrpcChannelOptions
        {
            HttpHandler = CreateHandler(identity, expectedPeer, endPoint),
            DisposeHttpClient = true,
        });
    }

    private static SocketsHttpHandler CreateHandler(DeviceIdentity identity, DeviceId expectedPeer, EndPoint? connectTo)
    {
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

        if (connectTo != null)
        {
            handler.ConnectCallback = async (_, cancellationToken) =>
            {
                var socket = connectTo.AddressFamily == AddressFamily.Unspecified
                    ? new Socket(SocketType.Stream, ProtocolType.Tcp) // DnsEndPoint: dual mode
                    : new Socket(connectTo.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    socket.NoDelay = true;
                    await socket.ConnectAsync(connectTo, cancellationToken);
                    return new NetworkStream(socket, true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            };
        }

        return handler;
    }
}

/// <summary>
/// TLS server names (SNI) that tell the Peer listener what a connection is for.
/// </summary>
internal static class PeerTlsServerNames
{
    /// <summary>
    /// A connection between Peers. Only paired Devices complete the TLS handshake.
    /// </summary>
    public const string Peer = "peer.clipmate.invalid";

    /// <summary>
    /// A connection for Pairing. Any Device with a certificate completes the TLS handshake, but only the pairing calls are served to it.
    /// </summary>
    public const string Pairing = "pairing.clipmate.invalid";
}

/// <summary>
/// Parses Peer addresses written as <c>host:port</c> (IPv6 addresses in brackets).
/// </summary>
public static class PeerAddress
{
    /// <summary>
    /// Parses <paramref name="address" /> into an <see cref="IPEndPoint" /> or, for a host name, a <see cref="DnsEndPoint" />.
    /// </summary>
    /// <exception cref="FormatException">The address has no valid port.</exception>
    public static EndPoint Parse(string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        var text = address.Trim();
        if (IPEndPoint.TryParse(text, out var ip) && ip.Port != 0)
            return ip;

        var colon = text.LastIndexOf(':');
        if (colon <= 0 || !int.TryParse(text[(colon + 1)..], out var port) || port is < 1 or > IPEndPoint.MaxPort)
            throw new FormatException($"'{address}' is not a valid address; expected host:port.");

        return new DnsEndPoint(text[..colon].Trim('[', ']'), port);
    }

    /// <summary>
    /// Gets the port of an endpoint returned by <see cref="Parse" />.
    /// </summary>
    public static int GetPort(EndPoint endPoint) => endPoint switch
    {
        IPEndPoint ip => ip.Port,
        DnsEndPoint dns => dns.Port,
        var _ => throw new ArgumentException("Unsupported endpoint type.", nameof(endPoint)),
    };
}
