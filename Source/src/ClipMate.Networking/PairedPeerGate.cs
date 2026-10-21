using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using ClipMate.Networking.Protocol;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ClipMate.Networking;

/// <summary>
/// The authorization gate for every call the Peer listener serves: only a paired Peer may call anything except the
/// pairing calls. Connections made for Pairing let an unpaired Device through the TLS handshake, so this check is what
/// keeps such a Device to the pairing calls.
/// </summary>
internal sealed class PairedPeerGate : Interceptor
{
    private static readonly HashSet<string> _pairingMethods =
    [
        $"/{PeerService.Descriptor.FullName}/{nameof(PeerService.PeerServiceBase.RequestPairing)}",
        $"/{PeerService.Descriptor.FullName}/{nameof(PeerService.PeerServiceBase.ConfirmPairing)}",
        $"/{PeerService.Descriptor.FullName}/{nameof(PeerService.PeerServiceBase.CheckPairing)}",
    ];

    private readonly ILogger<PairedPeerGate> _logger;
    private readonly IPeerCertificateValidator _validator;

    public PairedPeerGate(IPeerCertificateValidator validator, ILogger<PairedPeerGate> logger)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Returns <c>true</c> if <paramref name="method" /> (a full gRPC method name) may be called by a Device that is not a Peer.
    /// </summary>
    public static bool IsPairingMethod(string method) => _pairingMethods.Contains(method);

    /// <inheritdoc />
    public override Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request, ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        Authorize(context);
        return continuation(request, context);
    }

    /// <inheritdoc />
    public override Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context, ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        Authorize(context);
        return continuation(requestStream, context);
    }

    /// <inheritdoc />
    public override Task ServerStreamingServerHandler<TRequest, TResponse>(TRequest request, IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context, ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        Authorize(context);
        return continuation(request, responseStream, context);
    }

    /// <inheritdoc />
    public override Task DuplexStreamingServerHandler<TRequest, TResponse>(IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream, ServerCallContext context, DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        Authorize(context);
        return continuation(requestStream, responseStream, context);
    }

    private void Authorize(ServerCallContext context)
    {
        var caller = PeerCallContext.GetCallerDeviceId(context);
        if (IsPairingMethod(context.Method) || _validator.IsTrusted(caller))
            return;

        _logger.LogWarning("Refused {Method} from Device {DeviceId}, which is not a Peer", context.Method, caller.ToDisplayString());
        throw new RpcException(new Status(StatusCode.PermissionDenied, "This Device is not a Peer."));
    }
}

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
    public static System.Net.IPAddress? GetRemoteAddress(ServerCallContext context)
    {
        var address = context.GetHttpContext().Connection.RemoteIpAddress;
        return address is { IsIPv4MappedToIPv6: true } ? address.MapToIPv4() : address;
    }
}
