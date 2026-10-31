using System.Net;
using ClipMate.Core.Models.Networking;

namespace ClipMate.Networking;

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
    /// Gets the addresses to try to reach <paramref name="peer" /> at: the address it currently announces on the LAN
    /// (if it is among <paramref name="discoveredDevices" />), then its last known addresses.
    /// </summary>
    public static IEnumerable<string> GetAddressesToTry(Peer peer, IReadOnlyList<DiscoveredDevice> discoveredDevices)
    {
        ArgumentNullException.ThrowIfNull(peer);
        ArgumentNullException.ThrowIfNull(discoveredDevices);
        return GetAddressesToTryIterator(peer, discoveredDevices);
    }

    private static IEnumerable<string> GetAddressesToTryIterator(Peer peer, IReadOnlyList<DiscoveredDevice> discoveredDevices)
    {
        var announced = discoveredDevices.FirstOrDefault(p => p.DeviceId == peer.DeviceId)?.Endpoint.ToString();
        if (announced != null)
            yield return announced;

        foreach (var item in peer.Addresses)
        {
            if (!string.Equals(item, announced, StringComparison.OrdinalIgnoreCase))
                yield return item;
        }
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
