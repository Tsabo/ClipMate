using System.Net;

namespace ClipMate.Networking.Discovery;

/// <summary>
/// A received discovery datagram and where it came from.
/// </summary>
public readonly record struct DiscoveryDatagram(ReadOnlyMemory<byte> Data, IPEndPoint Source);

/// <summary>
/// Sends discovery datagrams to every Device on the LAN and receives theirs.
/// The UDP implementation uses IPv4 broadcast plus IPv6 multicast; tests use an in-memory one.
/// </summary>
public interface IDiscoveryTransport : IAsyncDisposable
{
    /// <summary>
    /// Sends <paramref name="datagram" /> to every Device on the LAN.
    /// </summary>
    Task SendAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken);

    /// <summary>
    /// Waits for the next datagram.
    /// </summary>
    Task<DiscoveryDatagram> ReceiveAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Opens <see cref="IDiscoveryTransport" />s.
/// </summary>
public interface IDiscoveryTransportFactory
{
    /// <summary>
    /// Opens a transport that sends to and listens on UDP <paramref name="port" />.
    /// </summary>
    IDiscoveryTransport Create(int port);
}
