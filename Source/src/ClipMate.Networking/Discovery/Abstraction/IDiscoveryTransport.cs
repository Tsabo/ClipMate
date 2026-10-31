namespace ClipMate.Networking.Discovery.Abstraction;

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
