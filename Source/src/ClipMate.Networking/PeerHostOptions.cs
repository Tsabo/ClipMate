using System.Net;
using ClipMate.Core.Models.Search;

namespace ClipMate.Networking;

/// <summary>
/// Settings for the Peer listener that are not user configuration.
/// </summary>
public sealed class PeerHostOptions
{
    /// <summary>
    /// Gets the address the listener binds to. Defaults to all IPv4 and IPv6 interfaces.
    /// </summary>
    public IPAddress ListenAddress { get; init; } = IPAddress.IPv6Any;

    /// <summary>
    /// Gets how long one Remote Search may run before the Peer stops it. Defaults to
    /// <see cref="RemoteSearchLimits.Timeout" />.
    /// </summary>
    public TimeSpan SearchTimeout { get; init; } = RemoteSearchLimits.Timeout;
}
