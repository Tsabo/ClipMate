using ClipMate.Core.Models;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Models.Search;
using ClipMate.Core.Services.Abstractions;

namespace ClipMate.Tests.Unit.Fakes;

/// <summary>
/// In-memory <see cref="IDatabases" /> for App tests: add Databases with <see cref="AddLocal" /> or
/// <see cref="AddRemote" />, fill them, and raise <see cref="RaiseChanged" />. Behaves as the real one does for an
/// unknown key: <see cref="DatabaseUnavailableException" />.
/// </summary>
public sealed class FakeDatabases : IDatabases
{
    private readonly List<FakeDatabase> _databases = [];

    /// <inheritdoc />
    public IReadOnlyList<IDatabase> All => _databases.Where(p => p.Remote == null).Concat(_databases.Where(p => p.Remote != null)).ToList();

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public IDatabase Get(string databaseKey) =>
        _databases.FirstOrDefault(p => p.Key == databaseKey) ?? throw new DatabaseUnavailableException($"No Database '{databaseKey}'.");

    /// <summary>
    /// Adds a local Database.
    /// </summary>
    public FakeDatabase AddLocal(string key, string name)
    {
        var database = new FakeDatabase(key, name, null);
        _databases.Add(database);
        return database;
    }

    /// <summary>
    /// Adds a Remote Database, read-only and available while <paramref name="remote" /> is online.
    /// </summary>
    public FakeDatabase AddRemote(RemoteDatabase remote)
    {
        ArgumentNullException.ThrowIfNull(remote);
        var database = new FakeDatabase(RemoteDatabaseKey.From(remote.Address), remote.Name, remote);
        _databases.Add(database);
        return database;
    }

    /// <summary>
    /// Removes a Database, as when it is unconfigured or no longer shared.
    /// </summary>
    public void Remove(string key) => _databases.RemoveAll(p => p.Key == key);

    /// <summary>
    /// Raises <see cref="Changed" />.
    /// </summary>
    public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// In-memory <see cref="IDatabase" />. Its tree, clip lists, contents and search results are set directly; while
/// <see cref="IsAvailable" /> is false every read throws <see cref="DatabaseUnavailableException" />, and a Remote one
/// has no Trash or folder-including lists, as the real ones do.
/// </summary>
public sealed class FakeDatabase : IDatabase
{
    private readonly Dictionary<Guid, ClipContent> _contents = [];
    private readonly Dictionary<ClipListScope, List<Clip>> _lists = [];

    internal FakeDatabase(string key, string name, RemoteDatabase? remote)
    {
        Key = key;
        Name = name;
        Remote = remote;
        IsAvailable = remote?.IsAvailable ?? true;
        IsReadOnly = remote != null;
    }

    /// <inheritdoc />
    public string Key { get; }

    /// <inheritdoc />
    public string Name { get; set; }

    /// <inheritdoc />
    public bool IsAvailable { get; set; }

    /// <inheritdoc />
    public bool IsReadOnly { get; set; }

    /// <inheritdoc />
    public RemoteDatabase? Remote { get; }

    /// <summary>
    /// Gets or sets the collections and folders <see cref="GetTreeAsync" /> returns.
    /// </summary>
    public List<CollectionEntry> Tree { get; set; } = [];

    /// <summary>
    /// Gets or sets what <see cref="SearchAsync" /> returns.
    /// </summary>
    public SearchMatches SearchResults { get; set; } = new([], 0);

    /// <summary>
    /// Gets the filters of every search, in order.
    /// </summary>
    public List<SearchFilters> Searches { get; } = [];

    /// <summary>
    /// Gets the scope and offset of every page asked for, in order.
    /// </summary>
    public List<(ClipListScope Scope, int Offset)> PageRequests { get; } = [];

    /// <summary>
    /// Sets the clips in <paramref name="scope" />, in display order.
    /// </summary>
    public FakeDatabase WithClips(ClipListScope scope, params Clip[] clips)
    {
        _lists[scope] = [.. clips];
        return this;
    }

    /// <summary>
    /// Sets a clip's content.
    /// </summary>
    public FakeDatabase WithContent(ClipContent content)
    {
        _contents[content.Clip.Id] = content;
        return this;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CollectionEntry>> GetTreeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfUnavailable();
        return Task.FromResult<IReadOnlyList<CollectionEntry>>(Tree.ToList());
    }

    /// <inheritdoc />
    public Task<ClipPage> GetClipPageAsync(ClipListScope scope, int offset, int count, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        PageRequests.Add((scope, offset));
        ThrowIfUnavailable();
        if (Remote != null && scope is ClipListScope.Trash or ClipListScope.Collection { IncludeFolders: true })
            throw new NotSupportedException("A Remote Database lists only the clips of one collection or folder at a time.");

        var clips = _lists.GetValueOrDefault(scope, []);
        return Task.FromResult(new ClipPage(clips.Skip(offset).Take(count).ToList(), offset, clips.Count));
    }

    /// <inheritdoc />
    public Task<ClipContent?> GetClipContentAsync(Guid clipId, CancellationToken cancellationToken = default)
    {
        ThrowIfUnavailable();
        return Task.FromResult(_contents.GetValueOrDefault(clipId));
    }

    /// <inheritdoc />
    public Task<SearchMatches> SearchAsync(SearchFilters filters, int? maxResults = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filters);
        ThrowIfUnavailable();
        Searches.Add(filters);
        var results = maxResults is { } most && SearchResults.Clips.Count > most
            ? SearchResults with { Clips = SearchResults.Clips.Take(most).ToList() }
            : SearchResults;

        return Task.FromResult(results);
    }

    private void ThrowIfUnavailable()
    {
        if (!IsAvailable)
            throw new DatabaseUnavailableException($"{Name} is unavailable.");
    }
}
