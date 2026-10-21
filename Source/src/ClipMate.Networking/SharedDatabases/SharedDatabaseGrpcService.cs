using ClipMate.Core.Services;
using ClipMate.Networking.Protocol;
using Grpc.Core;

namespace ClipMate.Networking.SharedDatabases;

/// <summary>
/// Serves this Device's Shared Databases to its Peers. <see cref="PairedPeerGate" /> refuses every call from a Device
/// that is not a Peer; <see cref="ISharedDatabaseService" /> decides what is shared.
/// </summary>
internal sealed class SharedDatabaseGrpcService : SharedDatabaseAccess.SharedDatabaseAccessBase
{
    private readonly ISharedDatabaseService _sharedDatabases;

    public SharedDatabaseGrpcService(ISharedDatabaseService sharedDatabases)
    {
        _sharedDatabases = sharedDatabases ?? throw new ArgumentNullException(nameof(sharedDatabases));
    }

    /// <inheritdoc />
    public override async Task<ListSharedDatabasesReply> ListSharedDatabases(ListSharedDatabasesRequest request, ServerCallContext context)
    {
        var databases = await _sharedDatabases.GetSharedDatabasesAsync(context.CancellationToken);
        var reply = new ListSharedDatabasesReply();
        reply.Databases.AddRange(databases.Select(SharedDatabaseMessages.ToMessage));
        return reply;
    }

    /// <inheritdoc />
    public override async Task<GetCollectionTreeReply> GetCollectionTree(GetCollectionTreeRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.DatabaseId, out var databaseId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid Database ID."));

        var collections = await _sharedDatabases.GetCollectionTreeAsync(databaseId, context.CancellationToken)
                          ?? throw new RpcException(new Status(StatusCode.NotFound, "This Database is not shared."));

        var reply = new GetCollectionTreeReply();
        reply.Collections.AddRange(collections.Select(SharedDatabaseMessages.ToMessage));
        return reply;
    }
}
