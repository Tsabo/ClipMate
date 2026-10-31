using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Models.Search;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services.Abstractions;

namespace ClipMate.Data.Services;

/// <summary>
/// <see cref="IDatabase" /> for a configured local Database, read through its repositories. Never reads the
/// decrypted-content cache, so encrypted clips come back as stored. A Database whose file is missing is unavailable
/// rather than created.
/// </summary>
internal sealed class LocalDatabase : IDatabase
{
    private readonly DatabaseConfiguration _configuration;
    private readonly IDatabaseContextFactory _databaseContextFactory;
    private readonly ISearchService _searchService;

    public LocalDatabase(string key,
        DatabaseConfiguration configuration,
        IDatabaseContextFactory databaseContextFactory,
        ISearchService searchService)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key;
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _databaseContextFactory = databaseContextFactory ?? throw new ArgumentNullException(nameof(databaseContextFactory));
        _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
    }

    /// <inheritdoc />
    public string Key { get; }

    /// <inheritdoc />
    public string Name => _configuration.Name;

    /// <inheritdoc />
    public bool IsAvailable => File.Exists(Environment.ExpandEnvironmentVariables(_configuration.FilePath));

    /// <inheritdoc />
    public bool IsReadOnly => false;

    /// <inheritdoc />
    public RemoteDatabase? Remote => null;

    /// <inheritdoc />
    public async Task<IReadOnlyList<CollectionEntry>> GetTreeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfUnavailable();
        var collections = await _databaseContextFactory.GetCollectionRepository(Key).GetAllAsync(cancellationToken);
        return collections.Select(CollectionEntry.From).ToList();
    }

    /// <inheritdoc />
    public async Task<ClipPage> GetClipPageAsync(ClipListScope scope, int offset, int count, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        ThrowIfUnavailable();

        var clips = _databaseContextFactory.GetClipRepository(Key);
        switch (scope)
        {
            case ClipListScope.Folder folder:
                return await clips.GetPageByFolderAsync(folder.FolderId, offset, count, cancellationToken);

            case ClipListScope.Trash:
                return Page(await clips.GetDeletedAsync(cancellationToken), offset, count);

            case ClipListScope.Collection { IncludeFolders: true } collection:
                return Page(await GetWithFoldersAsync(clips, collection.CollectionId, cancellationToken), offset, count);

            case ClipListScope.Collection collection:
                // Smart and SQL-based collections run their own stored SQL, only for the page asked for
                var entry = await _databaseContextFactory.GetCollectionRepository(Key).GetByIdAsync(collection.CollectionId, cancellationToken);
                if (entry is { IsVirtual: true } && !string.IsNullOrWhiteSpace(entry.Sql))
                    return await clips.ExecuteSqlPageAsync(VirtualCollectionSql.ReplacePlaceholders(entry.Sql, entry.RetentionLimit), offset, count, cancellationToken);

                return await clips.GetPageByCollectionAsync(collection.CollectionId, offset, count, cancellationToken);

            default:
                throw new NotSupportedException($"Unknown clip list scope {scope.GetType().Name}.");
        }
    }

    /// <inheritdoc />
    public async Task<ClipContent?> GetClipContentAsync(Guid clipId, CancellationToken cancellationToken = default)
    {
        ThrowIfUnavailable();
        var clip = await _databaseContextFactory.GetClipRepository(Key).GetByIdAsync(clipId, cancellationToken);
        if (clip == null)
            return null;

        var formats = await _databaseContextFactory.GetClipDataRepository(Key).GetByClipIdAsync(clipId, cancellationToken);
        var blobRepository = _databaseContextFactory.GetBlobRepository(Key);
        var textBlobs = await blobRepository.GetTextByClipIdAsync(clipId, cancellationToken);
        var jpgBlobs = await blobRepository.GetJpgByClipIdAsync(clipId, cancellationToken);
        var pngBlobs = await blobRepository.GetPngByClipIdAsync(clipId, cancellationToken);
        var binaryBlobs = await blobRepository.GetBlobByClipIdAsync(clipId, cancellationToken);

        return new ClipContent(clip, formats, textBlobs, jpgBlobs, pngBlobs, binaryBlobs);
    }

    /// <inheritdoc />
    public Task<SearchMatches> SearchAsync(SearchFilters filters, int? maxResults = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filters);
        ThrowIfUnavailable();
        return _searchService.SearchDatabaseAsync(Key, filters, maxResults, cancellationToken);
    }

    /// <summary>
    /// The clips of a collection and of all its folders, at any depth.
    /// </summary>
    private async Task<List<Clip>> GetWithFoldersAsync(IClipRepository clips, Guid collectionId, CancellationToken cancellationToken)
    {
        var result = new List<Clip>(await clips.GetByCollectionAsync(collectionId, cancellationToken));
        foreach (var item in await _databaseContextFactory.GetFolderRepository(Key).GetByCollectionAsync(collectionId, cancellationToken))
            result.AddRange(await clips.GetByFolderAsync(item.Id, cancellationToken));

        // A clip filed in a folder of the collection is in both lists
        return result.Where(p => !p.Del).DistinctBy(p => p.Id).ToList();
    }

    /// <summary>
    /// Pages a list that is already in display order.
    /// </summary>
    private static ClipPage Page(IReadOnlyList<Clip> clips, int offset, int count) =>
        new(clips.Skip(offset).Take(count).ToList(), offset, clips.Count);

    private void ThrowIfUnavailable()
    {
        if (!IsAvailable)
            throw new DatabaseUnavailableException($"The file of Database '{Name}' is missing: {_configuration.FilePath}");
    }
}
