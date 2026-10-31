using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Channels;
using ClipMate.Networking.Discovery.Abstraction;
using Microsoft.Extensions.Logging;

namespace ClipMate.Networking.Discovery.Transport;

/// <summary>
/// Settings for the UDP discovery transport.
/// </summary>
public sealed class UdpDiscoveryTransportOptions
{
    /// <summary>
    /// The link-local IPv6 multicast group ClipMate Devices announce to.
    /// </summary>
    public static readonly IPAddress DefaultIPv6Group = IPAddress.Parse("ff02::636c:6970");

    /// <summary>
    /// Gets the IPv6 multicast group to send to and join.
    /// </summary>
    public IPAddress IPv6Group { get; init; } = DefaultIPv6Group;

    /// <summary>
    /// Gets fixed endpoints to send to instead of IPv4 broadcast and IPv6 multicast. When set, no multicast group is joined.
    /// Tests use this to keep all traffic on loopback.
    /// </summary>
    public IReadOnlyList<IPEndPoint>? SendTargets { get; init; }
}

/// <summary>
/// Creates <see cref="UdpDiscoveryTransport" />s.
/// </summary>
public sealed class UdpDiscoveryTransportFactory : IDiscoveryTransportFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly UdpDiscoveryTransportOptions _options;

    /// <summary>
    /// Creates a factory for transports that use IPv4 broadcast and IPv6 multicast.
    /// </summary>
    public UdpDiscoveryTransportFactory(ILoggerFactory loggerFactory)
        : this(new UdpDiscoveryTransportOptions(), loggerFactory)
    {
    }

    /// <summary>
    /// Creates a factory with explicit transport options.
    /// </summary>
    public UdpDiscoveryTransportFactory(UdpDiscoveryTransportOptions options, ILoggerFactory loggerFactory)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    /// <inheritdoc />
    public IDiscoveryTransport Create(int port) => new UdpDiscoveryTransport(port, _options, _loggerFactory.CreateLogger<UdpDiscoveryTransport>());
}

/// <summary>
/// Discovery over UDP: sends to the IPv4 broadcast address of every active interface and to a link-local IPv6
/// multicast group on every active interface, and listens on the same port for both.
/// </summary>
internal sealed class UdpDiscoveryTransport : IDiscoveryTransport
{
    private const int _sioUdpConnectionReset = -1744830452; // SIO_UDP_CONNRESET

    private readonly CancellationTokenSource _cancellation = new();
    private readonly Channel<DiscoveryDatagram> _inbox =
        Channel.CreateBounded<DiscoveryDatagram>(new BoundedChannelOptions(256) { FullMode = BoundedChannelFullMode.DropOldest });

    private readonly HashSet<int> _joinedInterfaces = [];
    private readonly ILogger<UdpDiscoveryTransport> _logger;
    private readonly UdpDiscoveryTransportOptions _options;
    private readonly int _port;
    private readonly Task[] _receiveLoops;
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly Socket _v4;
    private readonly Socket? _v6;
    private bool _disposed;

    public UdpDiscoveryTransport(int port, UdpDiscoveryTransportOptions options, ILogger<UdpDiscoveryTransport> logger)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, IPEndPoint.MaxPort);
        _port = port;
        _options = options;
        _logger = logger;

        _v4 = OpenSocket(AddressFamily.InterNetwork, IPAddress.Any, port);
        _v4.EnableBroadcast = true;

        if (Socket.OSSupportsIPv6)
        {
            try
            {
                _v6 = OpenSocket(AddressFamily.InterNetworkV6, IPAddress.IPv6Any, port); // IPv6 only (the Windows default)
            }
            catch (SocketException ex)
            {
                _logger.LogWarning(ex, "IPv6 discovery is unavailable; using IPv4 only");
                _v6 = null;
            }
        }

        if (_options.SendTargets == null)
            JoinMulticastGroups();

        _receiveLoops = _v6 == null
            ? [Task.Run(() => ReceiveLoopAsync(_v4))]
            : [Task.Run(() => ReceiveLoopAsync(_v4)), Task.Run(() => ReceiveLoopAsync(_v6))];
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await _cancellation.CancelAsync();
        _v4.Dispose();
        _v6?.Dispose();
        try
        {
            await Task.WhenAll(_receiveLoops);
        }
        catch (OperationCanceledException)
        {
            // Expected.
        }

        _inbox.Writer.TryComplete();
        _cancellation.Dispose();
        _sendGate.Dispose();
    }

    /// <inheritdoc />
    public async Task<DiscoveryDatagram> ReceiveAsync(CancellationToken cancellationToken) =>
        await _inbox.Reader.ReadAsync(cancellationToken);

    /// <inheritdoc />
    public async Task SendAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _sendGate.WaitAsync(cancellationToken);
        try
        {
            if (_options.SendTargets != null)
            {
                foreach (var item in _options.SendTargets)
                    await SendToAsync(datagram, item, null, cancellationToken);

                return;
            }

            JoinMulticastGroups(); // picks up interfaces that came up since the last announcement
            foreach (var (target, interfaceIndex) in GetBroadcastTargets())
                await SendToAsync(datagram, target, interfaceIndex, cancellationToken);
        }
        finally
        {
            _sendGate.Release();
        }
    }

    private static Socket OpenSocket(AddressFamily family, IPAddress address, int port)
    {
        var socket = new Socket(family, SocketType.Dgram, ProtocolType.Udp);
        try
        {
            // Several ClipMate processes (or Windows users) on one computer can all listen for announcements.
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            // Don't let an ICMP "port unreachable" for an earlier send fail the next receive.
            socket.IOControl(_sioUdpConnectionReset, [0, 0, 0, 0], null);
            socket.Bind(new IPEndPoint(address, port));
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static IEnumerable<NetworkInterface> GetActiveInterfaces() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(p => p.OperationalStatus == OperationalStatus.Up
                        && p.NetworkInterfaceType != NetworkInterfaceType.Loopback
                        && p.SupportsMulticast);

    private List<(IPEndPoint Target, int? InterfaceIndex)> GetBroadcastTargets()
    {
        var targets = new List<(IPEndPoint Target, int? InterfaceIndex)>();
        foreach (var item in GetActiveInterfaces())
        {
            var properties = item.GetIPProperties();
            foreach (var address in properties.UnicastAddresses)
            {
                if (address.Address.AddressFamily != AddressFamily.InterNetwork || address.IPv4Mask.Equals(IPAddress.Any))
                    continue;

                var broadcast = GetBroadcastAddress(address.Address, address.IPv4Mask);
                if (targets.All(p => !p.Target.Address.Equals(broadcast)))
                    targets.Add((new IPEndPoint(broadcast, _port), null));
            }

            if (_v6 != null && item.Supports(NetworkInterfaceComponent.IPv6))
                targets.Add((new IPEndPoint(_options.IPv6Group, _port), properties.GetIPv6Properties().Index));
        }

        if (targets.All(p => p.Target.AddressFamily != AddressFamily.InterNetwork))
            targets.Add((new IPEndPoint(IPAddress.Broadcast, _port), null));

        return targets;
    }

    private static IPAddress GetBroadcastAddress(IPAddress address, IPAddress mask)
    {
        var addressBytes = address.GetAddressBytes();
        var maskBytes = mask.GetAddressBytes();
        var broadcast = new byte[addressBytes.Length];
        for (var i = 0; i < broadcast.Length; i++)
            broadcast[i] = (byte)(addressBytes[i] | ~maskBytes[i]);

        return new IPAddress(broadcast);
    }

    private void JoinMulticastGroups()
    {
        if (_v6 == null)
            return;

        try
        {
            foreach (var item in GetActiveInterfaces().Where(p => p.Supports(NetworkInterfaceComponent.IPv6)))
            {
                var index = item.GetIPProperties().GetIPv6Properties().Index;
                if (_joinedInterfaces.Contains(index))
                    continue;

                try
                {
                    _v6.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.AddMembership, new IPv6MulticastOption(_options.IPv6Group, index));
                    _joinedInterfaces.Add(index);
                }
                catch (SocketException ex)
                {
                    _logger.LogDebug(ex, "Could not join the discovery multicast group on interface {InterfaceIndex}", index);
                }
            }
        }
        catch (NetworkInformationException ex)
        {
            _logger.LogDebug(ex, "Could not list network interfaces");
        }
    }

    private async Task SendToAsync(ReadOnlyMemory<byte> datagram, IPEndPoint target, int? interfaceIndex, CancellationToken cancellationToken)
    {
        var socket = target.AddressFamily == AddressFamily.InterNetworkV6 ? _v6 : _v4;
        if (socket == null)
            return;

        try
        {
            if (interfaceIndex.HasValue)
                socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.MulticastInterface, interfaceIndex.Value);

            await socket.SendToAsync(datagram, SocketFlags.None, target, cancellationToken);
        }
        catch (SocketException ex)
        {
            _logger.LogDebug(ex, "Could not send the discovery announcement to {Target}", target);
        }
    }

    private async Task ReceiveLoopAsync(Socket socket)
    {
        var buffer = new byte[DiscoveryAnnouncement.MaxDatagramSize + 1];
        var any = socket.AddressFamily == AddressFamily.InterNetworkV6
            ? new IPEndPoint(IPAddress.IPv6Any, 0)
            : new IPEndPoint(IPAddress.Any, 0);

        var token = _cancellation.Token;
        while (!token.IsCancellationRequested)
        {
            try
            {
                var result = await socket.ReceiveFromAsync(buffer, SocketFlags.None, any, token);
                if (result.ReceivedBytes > DiscoveryAnnouncement.MaxDatagramSize)
                    continue;

                _inbox.Writer.TryWrite(new DiscoveryDatagram(buffer.AsSpan(0, result.ReceivedBytes).ToArray(), (IPEndPoint)result.RemoteEndPoint));
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
            {
                return;
            }
            catch (SocketException ex)
            {
                if (token.IsCancellationRequested)
                    return;

                _logger.LogDebug(ex, "Discovery receive failed; continuing");
                await Task.Delay(100, CancellationToken.None);
            }
        }
    }
}
