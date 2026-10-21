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
    private readonly ICollectionService _collectionService;
    private readonly IConfigurationService _configurationService;
    private readonly IDatabaseIdService _databaseIdService;
    private readonly ILogger<SharedDatabaseService> _logger;

    public SharedDatabaseService(IConfigurationService configurationService,
        IDatabaseIdService databaseIdService,
        ICollectionService collectionService,
        ILogger<SharedDatabaseService> logger)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _databaseIdService = databaseIdService ?? throw new ArgumentNullException(nameof(databaseIdService));
        _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
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
