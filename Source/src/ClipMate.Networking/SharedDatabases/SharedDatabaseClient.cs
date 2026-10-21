using System.Runtime.CompilerServices;
using System.Text;
using ClipMate.Core.Constants;
using ClipMate.Core.Models;
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

    /// <summary>
    /// Asks <paramref name="peer" /> for one page of the clips (metadata only) of a collection or folder of one of its
    /// Shared Databases.
    /// </summary>
    /// <returns>The page, or <c>null</c> if the Peer does not share that Database, collection or folder (any more).</returns>
    /// <exception cref="RemoteDatabaseUnavailableException">The Peer could not be reached or did not answer in time.</exception>
    Task<ClipPage?> GetClipPageAsync(DeviceIdentity identity, Peer peer, Guid databaseId, Guid collectionId, Guid? folderId, int offset,
        int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches a clip's formats from <paramref name="peer" />, then streams their content in chunks.
    /// </summary>
    /// <returns>The clip and its content, or <c>null</c> if the Peer does not share that clip (any more).</returns>
    /// <exception cref="RemoteDatabaseUnavailableException">
    /// The Peer could not be reached, did not answer in time, or sent content that does not match its formats.
    /// </exception>
    Task<ClipContent?> GetClipContentAsync(DeviceIdentity identity, Peer peer, Guid databaseId, Guid clipId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Watches <paramref name="peer" /> for changes to its Shared Databases: yields each notice as it arrives, for as
    /// long as the Peer keeps the stream open or until <paramref name="cancellationToken" /> is cancelled. Notices carry
    /// IDs only, never clip data.
    /// </summary>
    /// <exception cref="RemoteDatabaseUnavailableException">The Peer could not be reached, or the stream broke (the Peer went away or unpaired this Device).</exception>
    /// <exception cref="NotSupportedException">The Peer's version of ClipMate does not send change notices.</exception>
    IAsyncEnumerable<PeerChangeNotice> WatchChangesAsync(DeviceIdentity identity, Peer peer, CancellationToken cancellationToken = default);
}

/// <summary>
/// <see cref="ISharedDatabaseClient" /> over gRPC. Tries the Peer's announced address, then its last known addresses.
/// Every call has a deadline, so a Peer that stops answering never holds up the caller for long.
/// </summary>
public sealed class GrpcSharedDatabaseClient : ISharedDatabaseClient
{
    /// <summary>
    /// The most content accepted for one clip, so a misbehaving Peer cannot exhaust this Device's memory.
    /// </summary>
    private const long _maxContentLength = 512L * 1024 * 1024;

    private readonly TimeSpan _contentTimeout;
    private readonly IDeviceDiscoveryService _discovery;
    private readonly ILogger<GrpcSharedDatabaseClient> _logger;
    private readonly TimeSpan _timeout;

    public GrpcSharedDatabaseClient(IDeviceDiscoveryService discovery, ILogger<GrpcSharedDatabaseClient> logger)
        : this(discovery, logger, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(2))
    {
    }

    /// <summary>
    /// Creates the client with other deadlines (for tests).
    /// </summary>
    /// <param name="discovery">Where the Peers' current addresses come from.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="timeout">The deadline of each call that returns a single reply.</param>
    /// <param name="contentTimeout">The deadline for streaming a clip's content; defaults to <paramref name="timeout" />.</param>
    internal GrpcSharedDatabaseClient(IDeviceDiscoveryService discovery, ILogger<GrpcSharedDatabaseClient> logger, TimeSpan timeout,
        TimeSpan? contentTimeout = null)
    {
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeout = timeout;
        _contentTimeout = contentTimeout ?? timeout;
    }

    /// <inheritdoc />
    public async Task<ClipPage?> GetClipPageAsync(DeviceIdentity identity, Peer peer, Guid databaseId, Guid collectionId, Guid? folderId,
        int offset, int count, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetClipPageRequest
            {
                DatabaseId = databaseId.ToString("D"),
                CollectionId = collectionId.ToString("D"),
                FolderId = folderId?.ToString("D") ?? string.Empty,
                Offset = offset,
                Count = count,
            };

            var reply = await CallAsync(identity, peer, _timeout,
                (client, deadline) => client.GetClipPageAsync(request, deadline: deadline, cancellationToken: cancellationToken).ResponseAsync,
                cancellationToken);
            return SharedDatabaseMessages.FromMessage(reply);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return null;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unimplemented)
        {
            throw new RemoteDatabaseUnavailableException($"{peer.DisplayName} needs a newer version of ClipMate to browse its clips.", ex);
        }
    }

    /// <inheritdoc />
    public async Task<ClipContent?> GetClipContentAsync(DeviceIdentity identity, Peer peer, Guid databaseId, Guid clipId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var formatsRequest = new GetClipFormatsRequest { DatabaseId = databaseId.ToString("D"), ClipId = clipId.ToString("D") };
            var formatsReply = await CallAsync(identity, peer, _timeout,
                (client, deadline) => client.GetClipFormatsAsync(formatsRequest, deadline: deadline, cancellationToken: cancellationToken).ResponseAsync,
                cancellationToken);

            var clip = SharedDatabaseMessages.FromMessage(formatsReply.Clip ?? new ClipSummary());
            if (clip == null || clip.Id != clipId)
                throw new RemoteDatabaseUnavailableException($"{peer.DisplayName} sent an invalid clip.");

            var formats = formatsReply.Formats.Select(p => SharedDatabaseMessages.FromMessage(p, clipId)).OfType<ClipData>()
                .GroupBy(p => p.Id).Select(p => p.First()).ToList();

            var contentRequest = new ReadClipContentRequest { DatabaseId = formatsRequest.DatabaseId, ClipId = formatsRequest.ClipId };
            var content = await CallAsync(identity, peer, _contentTimeout,
                (client, deadline) => ReadContentAsync(client.ReadClipContent(contentRequest, deadline: deadline, cancellationToken: cancellationToken),
                    formats, peer, cancellationToken),
                cancellationToken);

            return BuildContent(clip, formats, content);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return null;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unimplemented)
        {
            throw new RemoteDatabaseUnavailableException($"{peer.DisplayName} needs a newer version of ClipMate to show its clips.", ex);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<PeerChangeNotice> WatchChangesAsync(DeviceIdentity identity, Peer peer,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(peer);

        var (channel, call) = await OpenWatchAsync(identity, peer, cancellationToken);
        using var ownedChannel = channel;
        using var ownedCall = call;
        while (true)
        {
            ChangeNotice notice;
            try
            {
                if (!await call.ResponseStream.MoveNext(cancellationToken))
                    yield break;

                notice = call.ResponseStream.Current;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled && cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("Stopped watching.", ex, cancellationToken);
            }
            catch (RpcException ex)
            {
                throw new RemoteDatabaseUnavailableException($"{peer.DisplayName} stopped sending changes.", ex);
            }

            if (SharedDatabaseMessages.FromMessage(notice) is { } item)
                yield return item;
        }
    }

    /// <summary>
    /// Starts the <c>WatchChanges</c> call at each of the Peer's addresses until one answers with its response headers
    /// (sent as soon as the Peer is listening). Only connecting has a deadline; the stream itself stays open.
    /// </summary>
    private async Task<(GrpcChannel Channel, AsyncServerStreamingCall<ChangeNotice> Call)> OpenWatchAsync(DeviceIdentity identity, Peer peer,
        CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        foreach (var address in PeerAddress.GetAddressesToTry(peer, _discovery.DiscoveredDevices))
        {
            cancellationToken.ThrowIfCancellationRequested();
            GrpcChannel? channel = null;
            AsyncServerStreamingCall<ChangeNotice>? call = null;
            try
            {
                channel = PeerChannelFactory.CreateForPeer(address, identity, peer.DeviceId);
                call = new SharedDatabaseAccess.SharedDatabaseAccessClient(channel).WatchChanges(new WatchChangesRequest(),
                    cancellationToken: cancellationToken);
                await call.ResponseHeadersAsync.WaitAsync(_timeout, cancellationToken);
                return (channel, call);
            }
            catch (Exception ex)
            {
                call?.Dispose();
                channel?.Dispose();
                switch (ex)
                {
                    case RpcException { StatusCode: StatusCode.Cancelled } when cancellationToken.IsCancellationRequested:
                        throw new OperationCanceledException("Stopped watching.", ex, cancellationToken);
                    case OperationCanceledException:
                        throw;
                    case RpcException { StatusCode: StatusCode.Unimplemented }:
                        throw new NotSupportedException($"{peer.DisplayName} needs a newer version of ClipMate to send changes.", ex);
                    case RpcException { StatusCode: StatusCode.PermissionDenied }:
                        throw new RemoteDatabaseUnavailableException($"{peer.DisplayName} no longer accepts this Device as a Peer.", ex);
                    case RpcException { StatusCode: StatusCode.Unavailable or StatusCode.DeadlineExceeded } or TimeoutException or FormatException:
                        lastError = ex;
                        _logger.LogDebug(ex, "Peer {DeviceId} not reachable at {Address}", peer.DeviceId.ToShortDisplayString(), address);
                        break;
                    case RpcException:
                        throw new RemoteDatabaseUnavailableException($"{peer.DisplayName} did not accept the request for changes.", ex);
                    default:
                        throw;
                }
            }
        }

        throw new RemoteDatabaseUnavailableException($"{peer.DisplayName} cannot be reached.", lastError ?? new InvalidOperationException("No address."));
    }

    /// <summary>
    /// Reassembles the streamed chunks into one buffer per format, checking every chunk against the formats and lengths
    /// the Peer announced.
    /// </summary>
    private static async Task<Dictionary<Guid, byte[]>> ReadContentAsync(AsyncServerStreamingCall<ClipContentChunk> call,
        IReadOnlyList<ClipData> formats, Peer peer, CancellationToken cancellationToken)
    {
        using var ownedCall = call;
        var byId = formats.ToDictionary(p => p.Id);
        var buffers = new Dictionary<Guid, (byte[] Buffer, int Written)>();
        long total = 0;

        await foreach (var item in ownedCall.ResponseStream.ReadAllAsync(cancellationToken))
        {
            if (!Guid.TryParse(item.FormatId, out var formatId) || !byId.TryGetValue(formatId, out var format) ||
                format.StorageType != item.StorageType || item.TotalLength is < 0 or > int.MaxValue)
                throw new RemoteDatabaseUnavailableException($"{peer.DisplayName} sent content for an unknown format.");

            if (!buffers.TryGetValue(formatId, out var entry))
            {
                total += item.TotalLength;
                if (total > _maxContentLength)
                    throw new RemoteDatabaseUnavailableException($"This clip from {peer.DisplayName} is too large to show.");

                entry = (new byte[item.TotalLength], 0);
            }

            if (item.TotalLength != entry.Buffer.Length || entry.Written + item.Data.Length > entry.Buffer.Length)
                throw new RemoteDatabaseUnavailableException($"{peer.DisplayName} sent more content than it announced.");

            item.Data.Span.CopyTo(entry.Buffer.AsSpan(entry.Written));
            buffers[formatId] = (entry.Buffer, entry.Written + item.Data.Length);
        }

        if (buffers.Values.Any(p => p.Written != p.Buffer.Length))
            throw new RemoteDatabaseUnavailableException($"{peer.DisplayName} sent incomplete content.");

        return buffers.ToDictionary(p => p.Key, p => p.Value.Buffer);
    }

    private static ClipContent BuildContent(Clip clip, IReadOnlyList<ClipData> formats, Dictionary<Guid, byte[]> content)
    {
        var textBlobs = new List<BlobTxt>();
        var jpgBlobs = new List<BlobJpg>();
        var pngBlobs = new List<BlobPng>();
        var binaryBlobs = new List<BlobBlob>();

        foreach (var item in formats)
        {
            if (!content.TryGetValue(item.Id, out var data))
                continue;

            switch (item.StorageType)
            {
                case StorageType.Text:
                    textBlobs.Add(new BlobTxt { Id = Guid.NewGuid(), ClipId = clip.Id, ClipDataId = item.Id, Data = Encoding.UTF8.GetString(data) });
                    break;
                case StorageType.Jpeg:
                    jpgBlobs.Add(new BlobJpg { Id = Guid.NewGuid(), ClipId = clip.Id, ClipDataId = item.Id, Data = data });
                    break;
                case StorageType.Png:
                    pngBlobs.Add(new BlobPng { Id = Guid.NewGuid(), ClipId = clip.Id, ClipDataId = item.Id, Data = data });
                    break;
                default:
                    binaryBlobs.Add(new BlobBlob { Id = Guid.NewGuid(), ClipId = clip.Id, ClipDataId = item.Id, Data = data });
                    break;
            }
        }

        return new ClipContent(clip, formats, textBlobs, jpgBlobs, pngBlobs, binaryBlobs);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SharedDatabase>> ListSharedDatabasesAsync(DeviceIdentity identity, Peer peer,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var reply = await CallAsync(identity, peer, _timeout,
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
            var reply = await CallAsync(identity, peer, _timeout,
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
    private async Task<TReply> CallAsync<TReply>(DeviceIdentity identity, Peer peer, TimeSpan timeout,
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
                return await call(new SharedDatabaseAccess.SharedDatabaseAccessClient(channel), DateTime.UtcNow + timeout);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled && cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("The call was cancelled.", ex, cancellationToken);
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
