using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using ClipMate.Networking.Protocol;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace ClipMate.Networking.SharedDatabases;

/// <summary>
/// Calls a Peer's Shared Database service over a Peer connection.
/// </summary>
public interface ISharedDatabaseClient
{
    /// <summary>
    /// Asks <paramref name="peer" /> for its Shared Databases. A Peer too old to share Databases shares none.
    /// </summary>
    /// <exception cref="RemoteDatabaseUnavailableException">The Peer could not be reached.</exception>
    Task<IReadOnlyList<SharedDatabase>> ListSharedDatabasesAsync(DeviceIdentity identity, Peer peer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks <paramref name="peer" /> for the collection and folder tree of one of its Shared Databases.
    /// </summary>
    /// <returns>The tree, or <c>null</c> if the Peer does not share that Database (any more).</returns>
    /// <exception cref="RemoteDatabaseUnavailableException">The Peer could not be reached.</exception>
    Task<IReadOnlyList<SharedCollection>?> GetCollectionTreeAsync(DeviceIdentity identity, Peer peer, Guid databaseId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// <see cref="ISharedDatabaseClient" /> over gRPC. Tries the Peer's announced address, then its last known addresses.
/// Every call has a deadline, so a Peer that stops answering never holds up the caller for long.
/// </summary>
public sealed class GrpcSharedDatabaseClient : ISharedDatabaseClient
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);
    private readonly IDeviceDiscoveryService _discovery;
    private readonly ILogger<GrpcSharedDatabaseClient> _logger;

    public GrpcSharedDatabaseClient(IDeviceDiscoveryService discovery, ILogger<GrpcSharedDatabaseClient> logger)
    {
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SharedDatabase>> ListSharedDatabasesAsync(DeviceIdentity identity, Peer peer,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var reply = await CallAsync(identity, peer,
                (client, deadline) => client.ListSharedDatabasesAsync(new ListSharedDatabasesRequest(), deadline: deadline,
                    cancellationToken: cancellationToken).ResponseAsync, cancellationToken);
            return SharedDatabaseMessages.FromMessage(reply);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unimplemented)
        {
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SharedCollection>?> GetCollectionTreeAsync(DeviceIdentity identity, Peer peer, Guid databaseId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetCollectionTreeRequest { DatabaseId = databaseId.ToString("D") };
            var reply = await CallAsync(identity, peer,
                (client, deadline) => client.GetCollectionTreeAsync(request, deadline: deadline, cancellationToken: cancellationToken).ResponseAsync,
                cancellationToken);
            return SharedDatabaseMessages.FromMessage(reply);
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.NotFound or StatusCode.Unimplemented)
        {
            return null;
        }
    }

    /// <summary>
    /// Makes the call at each of the Peer's addresses until one answers. Any reply from the Peer, including an error
    /// status, ends the search; only failing to reach it moves on to the next address.
    /// </summary>
    private async Task<TReply> CallAsync<TReply>(DeviceIdentity identity, Peer peer,
        Func<SharedDatabaseAccess.SharedDatabaseAccessClient, DateTime, Task<TReply>> call, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(peer);

        Exception? lastError = null;
        foreach (var address in PeerAddress.GetAddressesToTry(peer, _discovery.DiscoveredDevices))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var channel = PeerChannelFactory.CreateForPeer(address, identity, peer.DeviceId);
                return await call(new SharedDatabaseAccess.SharedDatabaseAccessClient(channel), DateTime.UtcNow + _timeout);
            }
            catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
            {
                lastError = ex;
                _logger.LogDebug(ex, "Peer {DeviceId} not reachable at {Address}", peer.DeviceId.ToShortDisplayString(), address);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.PermissionDenied)
            {
                throw new RemoteDatabaseUnavailableException($"{peer.DisplayName} no longer accepts this Device as a Peer.", ex);
            }
            catch (FormatException ex)
            {
                lastError = ex; // a malformed address in the Peer record
            }
        }

        throw new RemoteDatabaseUnavailableException($"{peer.DisplayName} cannot be reached.", lastError ?? new InvalidOperationException("No address."));
    }
}
