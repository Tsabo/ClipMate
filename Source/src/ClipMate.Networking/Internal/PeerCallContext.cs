using System.Net;
using ClipMate.Core.Models.Networking;
using Grpc.Core;

namespace ClipMate.Networking.Internal;

/// <summary>
/// Reads who is calling from a gRPC call on the Peer listener.
/// </summary>
internal static class PeerCallContext
{
    /// <summary>
    /// Gets the calling Device's Device ID from its TLS client certificate.
    /// </summary>
    public static DeviceId GetCallerDeviceId(ServerCallContext context)
    {
        var certificate = context.GetHttpContext().Connection.ClientCertificate
                          ?? throw new RpcException(new Status(StatusCode.Unauthenticated, "A Device certificate is required."));

        return DeviceId.FromCertificate(certificate);
    }

    /// <summary>
    /// Gets the address the call came from, or <c>null</c> if unknown.
    /// </summary>
    public static IPAddress? GetRemoteAddress(ServerCallContext context)
    {
        var address = context.GetHttpContext().Connection.RemoteIpAddress;
        return address is { IsIPv4MappedToIPv6: true }
            ? address.MapToIPv4()
            : address;
    }
}
