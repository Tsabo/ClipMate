using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// <see cref="ISharedDatabaseService" /> over the configured Databases whose Share with Peers setting is on.
/// Reads the configuration on every call, so sharing or unsharing takes effect immediately.
/// </summary>
internal sealed class SharedDatabaseService : ISharedDatabaseService
{
    private readonly IClipContentService _clipContentService;
    private readonly IClipService _clipService;
    private readonly ICollectionService _collectionService;
    private readonly IConfigurationService _configurationService;
    private readonly IDatabaseContextFactory _databaseContextFactory;
    private readonly IDatabaseIdService _databaseIdService;
    private readonly ILogger<SharedDatabaseService> _logger;

    public SharedDatabaseService(IConfigurationService configurationService,
        IDatabaseIdService databaseIdService,
        ICollectionService collectionService,
        IClipService clipService,
        IClipContentService clipContentService,
        IDatabaseContextFactory databaseContextFactory,
        ILogger<SharedDatabaseService> logger)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _databaseIdService = databaseIdService ?? throw new ArgumentNullException(nameof(databaseIdService));
        _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
        _clipService = clipService ?? throw new ArgumentNullException(nameof(clipService));
        _clipContentService = clipContentService ?? throw new ArgumentNullException(nameof(clipContentService));
        _databaseContextFactory = databaseContextFactory ?? throw new ArgumentNullException(nameof(databaseContextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SharedDatabase>> GetSharedDatabasesAsync(CancellationToken cancellationToken = default)
    {
        var shared = await GetSharedDatabaseKeysAsync(cancellationToken);
        return shared.Select(p => new SharedDatabase(p.DatabaseId, p.Configuration.Name)).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SharedCollection>?> GetCollectionTreeAsync(Guid databaseId, CancellationToken cancellationToken = default)
    {
        var shared = await GetSharedDatabaseKeysAsync(cancellationToken);
        var match = shared.FirstOrDefault(p => p.DatabaseId == databaseId);
        if (match == null)
            return null;

        var collections = await _collectionService.GetAllByDatabaseKeyAsync(match.DatabaseKey, cancellationToken);
        return collections.Select(SharedCollection.From).ToList();
    }

    /// <inheritdoc />
    public async Task<ClipPage?> GetClipPageAsync(Guid databaseId, Guid collectionId, Guid? folderId, int offset, int count,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        var databaseKey = await FindSharedDatabaseKeyAsync(databaseId, cancellationToken);
        if (databaseKey == null)
            return null;

        // Only collections and folders of this Shared Database; an ID from any other Database is unknown here
        var collections = await _collectionService.GetAllByDatabaseKeyAsync(databaseKey, cancellationToken);
        var collection = collections.FirstOrDefault(p => p.Id == collectionId);
        if (collection == null)
            return null;

        if (folderId.HasValue)
        {
            if (collections.All(p => p.Id != folderId.Value))
                return null;

            return await _databaseContextFactory.GetClipRepository(databaseKey).GetPageByFolderAsync(folderId.Value, offset, count, cancellationToken);
        }

        // Smart and SQL-based collections run their own stored SQL, exactly as they do locally
        if (collection.IsVirtual && !string.IsNullOrWhiteSpace(collection.Sql))
        {
            var clips = await _clipService.ExecuteSqlQueryAsync(databaseKey, collection.Sql, collection.RetentionLimit, cancellationToken);
            return new ClipPage(clips.Skip(offset).Take(count).ToList(), offset, clips.Count);
        }

        return await _databaseContextFactory.GetClipRepository(databaseKey).GetPageByCollectionAsync(collectionId, offset, count, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ClipContent?> GetClipContentAsync(Guid databaseId, Guid clipId, CancellationToken cancellationToken = default)
    {
        var databaseKey = await FindSharedDatabaseKeyAsync(databaseId, cancellationToken);
        if (databaseKey == null)
            return null;

        // Served as stored: an encrypted clip stays ciphertext (this reads the database, never the decrypted cache)
        var content = await _clipContentService.GetClipContentAsync(databaseKey, clipId, cancellationToken);
        return content is { Clip.Del: false } ? content : null;
    }

    /// <inheritdoc />
    public async Task<DatabaseConfiguration?> FindDatabaseIdConflictAsync(string filePath, IEnumerable<DatabaseConfiguration> otherDatabases,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(otherDatabases);

        var databaseId = await _databaseIdService.GetDatabaseIdForFileAsync(filePath, cancellationToken);
        if (databaseId == null)
            return null;

        foreach (var item in otherDatabases.Where(p => p.ShareWithPeers && !string.IsNullOrWhiteSpace(p.FilePath)))
        {
            if (await _databaseIdService.GetDatabaseIdForFileAsync(item.FilePath, cancellationToken) == databaseId)
                return item;
        }

        return null;
    }

    private async Task<string?> FindSharedDatabaseKeyAsync(Guid databaseId, CancellationToken cancellationToken)
    {
        var shared = await GetSharedDatabaseKeysAsync(cancellationToken);
        return shared.FirstOrDefault(p => p.DatabaseId == databaseId)?.DatabaseKey;
    }

    /// <summary>
    /// The configured Databases that are shared and have a Database ID, without a second Database using the same ID.
    /// </summary>
    private async Task<List<SharedDatabaseKey>> GetSharedDatabaseKeysAsync(CancellationToken cancellationToken)
    {
        var result = new List<SharedDatabaseKey>();
        foreach (var (key, configuration) in _configurationService.Configuration.Databases.ToList())
        {
            if (!configuration.ShareWithPeers)
                continue;

            var databaseId = await _databaseIdService.GetDatabaseIdAsync(key, cancellationToken);
            if (databaseId == null)
                continue;

            // Sharing is refused for a Database whose ID is taken, but the configuration can still be edited by hand
            if (result.Any(p => p.DatabaseId == databaseId))
            {
                _logger.LogWarning("Not sharing Database {Name}: another Shared Database has the same Database ID {DatabaseId}",
                    configuration.Name, databaseId);
                continue;
            }

            result.Add(new SharedDatabaseKey(key, databaseId.Value, configuration));
        }

        return result;
    }

    private sealed record SharedDatabaseKey(string DatabaseKey, Guid DatabaseId, DatabaseConfiguration Configuration);
}
