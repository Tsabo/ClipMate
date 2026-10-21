using System.Collections.Concurrent;
using ClipMate.Core.Models.Networking;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;

namespace ClipMate.Networking;

/// <summary>
/// Keeps track of the open connections to the Peer listener by the connecting Device, so that unpairing a Device can
/// close its connections at once. (A TLS client certificate is only checked when a connection starts, so without this an
/// unpaired Device's open connections and calls would carry on.)
/// </summary>
internal sealed class PeerConnectionTracker
{
    private readonly ConcurrentDictionary<ConnectionContext, DeviceId> _connections = new();

    /// <summary>
    /// Connection middleware (after TLS): records the connection for the Device that presented the client certificate
    /// while the rest of the pipeline runs.
    /// </summary>
    public ConnectionDelegate Track(ConnectionDelegate next) => async context =>
    {
        var certificate = context.Features.Get<ITlsConnectionFeature>()?.ClientCertificate;
        if (certificate == null)
        {
            await next(context);
            return;
        }

        _connections[context] = DeviceId.FromCertificate(certificate);
        try
        {
            await next(context);
        }
        finally
        {
            _connections.TryRemove(context, out _);
        }
    };

    /// <summary>
    /// Closes every open connection from the Device with <paramref name="deviceId" />, ending its calls.
    /// </summary>
    /// <returns>The number of connections closed.</returns>
    public int Drop(DeviceId deviceId)
    {
        var dropped = 0;
        foreach (var item in _connections)
        {
            if (item.Value != deviceId)
                continue;

            item.Key.Abort(new ConnectionAbortedException("The Device was unpaired."));
            dropped++;
        }

        return dropped;
    }
}
