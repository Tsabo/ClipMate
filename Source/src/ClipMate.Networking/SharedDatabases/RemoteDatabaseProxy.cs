using ClipMate.Core.Models;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Models.Search;
using ClipMate.Core.Services.Abstractions;

namespace ClipMate.Networking.SharedDatabases;

/// <summary>
/// <see cref="IDatabase" /> for a Remote Database: every read is a coarse request its Peer runs against its own
/// Database (a read-only live proxy). There is no Trash, and nothing can be written.
/// </summary>
internal sealed class RemoteDatabaseProxy : IDatabase
{
    private readonly IRemoteDatabaseService _remoteDatabases;

    public RemoteDatabaseProxy(RemoteDatabase database, IRemoteDatabaseService remoteDatabases)
    {
        Remote = database ?? throw new ArgumentNullException(nameof(database));
        _remoteDatabases = remoteDatabases ?? throw new ArgumentNullException(nameof(remoteDatabases));
        Key = RemoteDatabaseKey.From(database.Address);
    }

    /// <inheritdoc />
    public string Key { get; }

    /// <inheritdoc />
    public string Name => Remote.Name;

    /// <inheritdoc />
    public bool IsAvailable => Remote.IsAvailable;

    /// <inheritdoc />
    public bool IsReadOnly => true;

    /// <inheritdoc />
    public RemoteDatabase Remote { get; }

    /// <inheritdoc />
    public Task<IReadOnlyList<CollectionEntry>> GetTreeAsync(CancellationToken cancellationToken = default) =>
        _remoteDatabases.GetCollectionTreeAsync(Remote.Address, cancellationToken);

    /// <inheritdoc />
    public Task<ClipPage> GetClipPageAsync(ClipListScope scope, int offset, int count, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return scope switch
        {
            ClipListScope.Collection { IncludeFolders: false } collection =>
                _remoteDatabases.GetClipPageAsync(Remote.Address, collection.CollectionId, null, offset, count, cancellationToken),
            ClipListScope.Folder folder =>
                _remoteDatabases.GetClipPageAsync(Remote.Address, folder.CollectionId, folder.FolderId, offset, count, cancellationToken),
            var _ => throw new NotSupportedException("A Remote Database lists only the clips of one collection or folder at a time."),
        };
    }

    /// <inheritdoc />
    public Task<ClipContent?> GetClipContentAsync(Guid clipId, CancellationToken cancellationToken = default) =>
        _remoteDatabases.GetClipContentAsync(Remote.Address, clipId, cancellationToken);

    /// <inheritdoc />
    public async Task<SearchMatches> SearchAsync(SearchFilters filters, int? maxResults = null, CancellationToken cancellationToken = default)
    {
        var matches = await _remoteDatabases.SearchAsync(Remote.Address, filters, cancellationToken);
        return maxResults is { } most && matches.Clips.Count > most
            ? matches with { Clips = matches.Clips.Take(most).ToList() }
            : matches;
    }
}
