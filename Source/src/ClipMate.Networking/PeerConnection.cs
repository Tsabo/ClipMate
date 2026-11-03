using System.Runtime.CompilerServices;
using ClipMate.Core.Models.Networking;
using ClipMate.Networking.SharedDatabases;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace ClipMate.Networking;

/// <summary>
/// How to reach one Peer, from <see cref="PeerConnector.ForPeer" />: this Device's identity, the Peer as recorded then,
/// and the addresses to try in order.
/// </summary>
public sealed class PeerConnection
{
    private readonly ILogger _logger;

    internal PeerConnection(DeviceIdentity identity, Peer peer, IReadOnlyList<string> addresses, ILogger logger)
    {
        Identity = identity;
        Peer = peer;
        Addresses = addresses;
        _logger = logger;
    }

    /// <summary>
    /// Gets the identity this Device connects as.
    /// </summary>
    public DeviceIdentity Identity { get; }

    /// <summary>
    /// Gets the Peer.
    /// </summary>
    public Peer Peer { get; }

    /// <summary>
    /// Gets the addresses to try: the one the Peer announces on the LAN, then its last known addresses.
    /// </summary>
    public IReadOnlyList<string> Addresses { get; }

    /// <summary>
    /// Runs <paramref name="attempt" /> at each address in turn until the Peer answers at one. An attempt that gets no
    /// answer returns <c>null</c> or throws an exception <see cref="IsNoAnswer" /> recognizes, and the next address is
    /// tried; any other exception is an answer too, and reaches the caller.
    /// </summary>
    /// <returns>The first answer and the address it came from, or <c>null</c> if the Peer answered at none of them.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was cancelled.</exception>
    public async Task<PeerAnswer<T>?> TryEachAddressAsync<T>(Func<string, CancellationToken, Task<T?>> attempt,
        CancellationToken cancellationToken)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(attempt);
        foreach (var address in Addresses)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (await attempt(address, cancellationToken) is { } answer)
                    return new PeerAnswer<T>(answer, address);
            }
            catch (Exception ex) when (cancellationToken.IsCancellationRequested && IsCancelledCall(ex))
            {
                throw new OperationCanceledException("The call was cancelled.", ex, cancellationToken);
            }
            catch (Exception ex) when (IsNoAnswer(ex))
            {
                _logger.LogDebug(ex, "Peer {DeviceId} not reachable at {Address}", Peer.DeviceId.ToShortDisplayString(), address);
            }
        }

        return null;
    }

    /// <summary>
    /// Makes <paramref name="call" /> over a Peer connection (<see cref="PeerChannelFactory.CreateForPeer" />) at each
    /// address in turn until the Peer answers, as <see cref="TryEachAddressAsync{T}" /> does. The channel is closed when
    /// the call completes.
    /// </summary>
    /// <returns>The Peer's reply, or <c>null</c> if the Peer answered at none of its addresses.</returns>
    public async Task<T?> CallAsync<T>(Func<GrpcChannel, CancellationToken, Task<T>> call, CancellationToken cancellationToken)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(call);
        var answer = await TryEachAddressAsync<T>(async (address, token) =>
        {
            using var channel = PeerChannelFactory.CreateForPeer(address, Identity, Peer.DeviceId);
            return await call(channel, token);
        }, cancellationToken);

        return answer?.Value;
    }

    /// <summary>
    /// Returns <c>true</c> if <paramref name="exception" /> means nothing answered at an address: the call got no reply
    /// (<see cref="PeerCallFailure.IsNoReply" />), timed out, or the address is malformed.
    /// </summary>
    /// <remarks>
    /// Neither this nor <see cref="IsCancelledCall" /> is inlined into the loop's exception filters, so the gRPC assemblies
    /// load only once a call has failed (a catch clause naming a gRPC type would load them as soon as the loop runs).
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool IsNoAnswer(Exception exception) => exception switch
    {
        RpcException rpc => PeerCallFailure.IsNoReply(rpc),
        TimeoutException or FormatException => true,
        var _ => false,
    };

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IsCancelledCall(Exception exception) => exception is RpcException { StatusCode: StatusCode.Cancelled };
}
