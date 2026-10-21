using System.Text;
using ClipMate.Core.Constants;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Search;
using ClipMate.Core.Services;
using Google.Protobuf;
using ClipMate.Networking.Protocol;
using Grpc.Core;

namespace ClipMate.Networking.SharedDatabases;

/// <summary>
/// Serves this Device's Shared Databases to its Peers. <see cref="PairedPeerGate" /> refuses every call from a Device
/// that is not a Peer; <see cref="ISharedDatabaseService" /> decides what is shared.
/// </summary>
internal sealed class SharedDatabaseGrpcService : SharedDatabaseAccess.SharedDatabaseAccessBase
{
    private readonly PeerHostOptions _options;
    private readonly ISharedDatabaseService _sharedDatabases;

    public SharedDatabaseGrpcService(ISharedDatabaseService sharedDatabases, PeerHostOptions options)
    {
        _sharedDatabases = sharedDatabases ?? throw new ArgumentNullException(nameof(sharedDatabases));
        _options = options ?? throw new ArgumentNullException(nameof(options));
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

    /// <inheritdoc />
    public override async Task<GetClipPageReply> GetClipPage(GetClipPageRequest request, ServerCallContext context)
    {
        var databaseId = ParseId(request.DatabaseId, "Database ID");
        var collectionId = ParseId(request.CollectionId, "collection ID");
        Guid? folderId = string.IsNullOrEmpty(request.FolderId) ? null : ParseId(request.FolderId, "folder ID");
        if (request.Offset < 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid offset."));

        var count = Math.Clamp(request.Count, 1, MaxPageSize);
        var page = await _sharedDatabases.GetClipPageAsync(databaseId, collectionId, folderId, request.Offset, count, context.CancellationToken)
                   ?? throw new RpcException(new Status(StatusCode.NotFound, "This collection is not shared."));

        var reply = new GetClipPageReply { Offset = page.Offset, TotalCount = page.TotalCount };
        reply.Clips.AddRange(page.Clips.Select(SharedDatabaseMessages.ToMessage));
        return reply;
    }

    /// <inheritdoc />
    public override async Task<GetClipFormatsReply> GetClipFormats(GetClipFormatsRequest request, ServerCallContext context)
    {
        var content = await GetContentAsync(request.DatabaseId, request.ClipId, context.CancellationToken);
        var reply = new GetClipFormatsReply { Clip = SharedDatabaseMessages.ToMessage(content.Clip) };
        reply.Formats.AddRange(content.Formats.Select(SharedDatabaseMessages.ToMessage));
        return reply;
    }

    /// <inheritdoc />
    public override async Task ReadClipContent(ReadClipContentRequest request, IServerStreamWriter<ClipContentChunk> responseStream,
        ServerCallContext context)
    {
        var content = await GetContentAsync(request.DatabaseId, request.ClipId, context.CancellationToken);
        foreach (var item in content.Formats)
        {
            var data = GetStoredContent(content, item);
            if (data == null)
                continue;

            var formatId = item.Id.ToString("D");
            var offset = 0;
            do
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                var length = Math.Min(ChunkSize, data.Length - offset);
                await responseStream.WriteAsync(new ClipContentChunk
                {
                    FormatId = formatId,
                    StorageType = item.StorageType,
                    Data = ByteString.CopyFrom(data, offset, length),
                    TotalLength = data.Length,
                }, context.CancellationToken);

                offset += length;
            } while (offset < data.Length);
        }
    }

    /// <inheritdoc />
    public override async Task SearchClips(SearchClipsRequest request, IServerStreamWriter<SearchClipsPage> responseStream, ServerCallContext context)
    {
        var databaseId = ParseId(request.DatabaseId, "Database ID");
        SearchFilters filters;
        try
        {
            filters = SharedDatabaseMessages.FromMessage(request.Criteria ?? new SearchCriteria());
        }
        catch (ArgumentException ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }

        // The search stops at the time limit or as soon as the caller cancels, whichever comes first
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        limit.CancelAfter(_options.SearchTimeout);

        SearchMatches matches;
        try
        {
            matches = await _sharedDatabases.SearchAsync(databaseId, filters, limit.Token)
                      ?? throw new RpcException(new Status(StatusCode.NotFound, "This Database, collection or folder is not shared."));
        }
        catch (OperationCanceledException) when (limit.IsCancellationRequested && !context.CancellationToken.IsCancellationRequested)
        {
            // Not DEADLINE_EXCEEDED, which the caller reads as "Peer not reachable at this address"
            throw new RpcException(new Status(StatusCode.ResourceExhausted, "The search took too long."));
        }
        catch (ArgumentException ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }

        // Never more than the cap, whatever the search returned; every page carries the total
        var clips = matches.Clips.Take(RemoteSearchLimits.MaxResults).ToList();
        var offset = 0;
        do
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var page = new SearchClipsPage { Offset = offset, TotalMatches = Math.Max(matches.TotalMatches, clips.Count) };
            page.Clips.AddRange(clips.Skip(offset).Take(SearchPageSize).Select(SharedDatabaseMessages.ToMessage));
            await responseStream.WriteAsync(page, context.CancellationToken);
            offset += SearchPageSize;
        } while (offset < clips.Count);
    }

    /// <summary>
    /// The most search results sent in one page.
    /// </summary>
    internal const int SearchPageSize = 200;

    /// <summary>
    /// The most clips served in one page.
    /// </summary>
    internal const int MaxPageSize = 500;

    /// <summary>
    /// The size of one piece of streamed content: well below gRPC's message size limit.
    /// </summary>
    internal const int ChunkSize = 64 * 1024;

    private async Task<ClipContent> GetContentAsync(string databaseIdText, string clipIdText, CancellationToken cancellationToken)
    {
        var databaseId = ParseId(databaseIdText, "Database ID");
        var clipId = ParseId(clipIdText, "clip ID");
        return await _sharedDatabases.GetClipContentAsync(databaseId, clipId, cancellationToken)
               ?? throw new RpcException(new Status(StatusCode.NotFound, "This clip is not shared."));
    }

    /// <summary>
    /// The stored bytes of one format (text as UTF-8), or <c>null</c> if the format has no stored content.
    /// </summary>
    private static byte[]? GetStoredContent(ClipContent content, ClipData format) => format.StorageType switch
    {
        StorageType.Text => content.TextBlobs.FirstOrDefault(p => p.ClipDataId == format.Id) is { } text ? Encoding.UTF8.GetBytes(text.Data) : null,
        StorageType.Jpeg => content.JpgBlobs.FirstOrDefault(p => p.ClipDataId == format.Id)?.Data,
        StorageType.Png => content.PngBlobs.FirstOrDefault(p => p.ClipDataId == format.Id)?.Data,
        StorageType.Binary => content.BinaryBlobs.FirstOrDefault(p => p.ClipDataId == format.Id)?.Data,
        var _ => null,
    };

    private static Guid ParseId(string text, string what) =>
        Guid.TryParse(text, out var id) ? id : throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid {what}."));
}
