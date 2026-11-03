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
    public Task<IReadOnlyList<CollectionEntry>> GetTreeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfUnavailable();
        return OnWorkerAsync<IReadOnlyList<CollectionEntry>>(async () =>
        {
            var collections = await _databaseContextFactory.GetCollectionRepository(Key).GetAllAsync(cancellationToken);
            return collections.Select(CollectionEntry.From).ToList();
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ClipPage> GetClipPageAsync(ClipListScope scope, int offset, int count, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        ThrowIfUnavailable();

        return OnWorkerAsync(() => ReadClipPageAsync(scope, offset, count, cancellationToken), cancellationToken);
    }

    private async Task<ClipPage> ReadClipPageAsync(ClipListScope scope, int offset, int count, CancellationToken cancellationToken)
    {
        var clips = _databaseContextFactory.GetClipRepository(Key);
        switch (scope)
        {
            case ClipListScope.Folder folder:
                return await clips.GetPageByFolderAsync(folder.FolderId, offset, count, cancellationToken);

            case ClipListScope.Trash:
                return await clips.GetDeletedPageAsync(offset, count, cancellationToken);

            case ClipListScope.Collection { IncludeFolders: true } collection:
                return await clips.GetPageWithFoldersAsync(collection.CollectionId, offset, count, cancellationToken);

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
    public Task<ClipContent?> GetClipContentAsync(Guid clipId, CancellationToken cancellationToken = default)
    {
        ThrowIfUnavailable();
        return OnWorkerAsync(() => ReadClipContentAsync(clipId, cancellationToken), cancellationToken);
    }

    private async Task<ClipContent?> ReadClipContentAsync(Guid clipId, CancellationToken cancellationToken)
    {
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
        return OnWorkerAsync(() => _searchService.SearchDatabaseAsync(Key, filters, maxResults, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Runs SQLite work on a worker thread. Microsoft.Data.Sqlite's async methods complete synchronously, so awaiting
    /// them on the caller's thread (the UI thread when a collection is clicked) would block it until the query ends.
    /// Each repository from the context factory has its own context, so reads may run at the same time.
    /// </summary>
    private static Task<T> OnWorkerAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken) =>
        Task.Run(work, cancellationToken);

    private void ThrowIfUnavailable()
    {
        if (!IsAvailable)
            throw new DatabaseUnavailableException($"The file of Database '{Name}' is missing: {_configuration.FilePath}");
    }
}
