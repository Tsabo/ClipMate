using System.Collections.Concurrent;
using System.Net;
using System.Threading.Channels;
using ClipMate.Networking.Discovery.Abstraction;

namespace ClipMate.Tests.Unit.Networking;

/// <summary>
/// A pretend LAN for discovery tests: every datagram any transport sends reaches every open transport, the sender included
/// (like a broadcast the sender also hears).
/// </summary>
internal sealed class InMemoryDiscoveryNetwork
{
    private readonly ConcurrentDictionary<Transport, byte> _transports = new();

    /// <summary>
    /// Every datagram sent, with its sender.
    /// </summary>
    public ConcurrentQueue<DiscoveryDatagram> Sent { get; } = new();

    /// <summary>
    /// Gets the number of transports currently open.
    /// </summary>
    public int OpenTransports => _transports.Count;

    /// <summary>
    /// Creates a transport factory for a Device at <paramref name="address" />.
    /// </summary>
    public IDiscoveryTransportFactory CreateFactory(IPAddress address) => new Factory(this, address);

    /// <summary>
    /// Delivers a datagram to every open transport as if <paramref name="source" /> had sent it.
    /// </summary>
    public void Deliver(byte[] datagram, IPEndPoint source)
    {
        foreach (var item in _transports.Keys)
            item.Inbox.Writer.TryWrite(new DiscoveryDatagram(datagram, source));
    }

    private sealed class Factory(InMemoryDiscoveryNetwork network, IPAddress address) : IDiscoveryTransportFactory
    {
        public IDiscoveryTransport Create(int port)
        {
            var transport = new Transport(network, new IPEndPoint(address, port));
            network._transports.TryAdd(transport, 0);
            return transport;
        }
    }

    private sealed class Transport(InMemoryDiscoveryNetwork network, IPEndPoint localEndpoint) : IDiscoveryTransport
    {
        public Channel<DiscoveryDatagram> Inbox { get; } = Channel.CreateUnbounded<DiscoveryDatagram>();

        public ValueTask DisposeAsync()
        {
            network._transports.TryRemove(this, out _);
            Inbox.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }

        public Task SendAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken)
        {
            if (!network._transports.ContainsKey(this))
                throw new ObjectDisposedException(nameof(Transport));

            var copy = datagram.ToArray();
            network.Sent.Enqueue(new DiscoveryDatagram(copy, localEndpoint));
            network.Deliver(copy, localEndpoint);
            return Task.CompletedTask;
        }

        public async Task<DiscoveryDatagram> ReceiveAsync(CancellationToken cancellationToken) =>
            await Inbox.Reader.ReadAsync(cancellationToken);
    }
}
