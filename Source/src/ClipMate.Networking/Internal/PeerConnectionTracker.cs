using System.Collections.Concurrent;
using ClipMate.Core.Models.Networking;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http.Features;

namespace ClipMate.Networking.Internal;

/// <summary>
/// Keeps track of the open connections to the Peer listener by the connecting Device, so that unpairing a Device can
/// close its connections at once. (A TLS client certificate is only checked when a connection starts, so without this an
/// unpaired Device's open connections and calls would carry on.)
/// </summary>
internal sealed class PeerConnectionTracker
{
    private readonly ConcurrentDictionary<string, byte> _closeGracefully = new();
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
            _connections.TryRemove(context, out var _);
            _closeGracefully.TryRemove(context.ConnectionId, out var _);
        }
    };

    /// <summary>
    /// Makes <see cref="Drop" /> close the connection with <paramref name="connectionId" /> gracefully, letting its calls
    /// finish: used for the connection on which the Peer itself says it unpaired this Device, so its reply still arrives.
    /// </summary>
    public void CloseGracefullyOnDrop(string connectionId) => _closeGracefully[connectionId] = 0;

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

            // The calls on a connection closed gracefully still pass PairedPeerGate one by one, so a new call is refused.
            if (_closeGracefully.ContainsKey(item.Key.ConnectionId))
                item.Key.Features.Get<IConnectionLifetimeNotificationFeature>()?.RequestClose();
            else
                item.Key.Abort(new ConnectionAbortedException("The Device was unpaired."));

            dropped++;
        }

        return dropped;
    }
}
