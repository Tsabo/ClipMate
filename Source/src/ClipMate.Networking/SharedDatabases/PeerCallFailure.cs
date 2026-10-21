using Grpc.Core;

namespace ClipMate.Networking.SharedDatabases;

/// <summary>
/// Tells apart the ways a call to a Peer can fail.
/// </summary>
internal static class PeerCallFailure
{
    /// <summary>
    /// Returns <c>true</c> if the call failed without a reply from the Peer: nothing answered at the address, the Peer did not
    /// answer in time, or the connection broke before the Peer sent a status (for example a TLS or HTTP/2 handshake the
    /// other end aborted, which the gRPC client reports as <see cref="StatusCode.Internal" /> with the local error attached).
    /// Such a call may be tried at the Peer's next address.
    /// </summary>
    public static bool IsNoReply(RpcException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception.StatusCode switch
        {
            StatusCode.Unavailable or StatusCode.DeadlineExceeded => true,
            StatusCode.Internal => exception.Status.DebugException != null, // a status sent by the Peer carries no local exception
            var _ => false,
        };
    }

    /// <summary>
    /// Describes, for the user, a status the Peer answered with that the caller has no specific message for.
    /// </summary>
    public static string Describe(RpcException exception, string peerName)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception.StatusCode switch
        {
            StatusCode.ResourceExhausted => $"{peerName} is too busy to answer right now. Try again later.",
            StatusCode.InvalidArgument => $"{peerName} could not handle the request: {exception.Status.Detail}",
            StatusCode.Unauthenticated => $"{peerName} did not accept this Device's certificate.",
            var _ => $"{peerName} could not answer the request (error: {exception.StatusCode}).",
        };
    }
}
