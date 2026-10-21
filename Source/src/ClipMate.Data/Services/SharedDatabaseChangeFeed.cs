using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// <see cref="ISharedDatabaseChangeFeed" /> over <see cref="DatabaseChangeMonitor" /> (every write through the normal
/// data path) and the configuration (sharing and unsharing). Writes to Databases that are not shared are ignored.
/// Listens to neither while nothing is subscribed.
/// </summary>
internal sealed class SharedDatabaseChangeFeed : ISharedDatabaseChangeFeed, IDisposable
{
    private readonly IConfigurationService _configurationService;
    private readonly IDatabaseContextFactory _contextFactory;
    private readonly IDatabaseIdService _databaseIdService;
    private readonly Lock _lock = new();
    private readonly ILogger<SharedDatabaseChangeFeed> _logger;
    private readonly DatabaseChangeMonitor _monitor;
    private EventHandler<SharedCollectionChangedEventArgs>? _collectionChanged;
    private bool _disposed;
    private string _shareSignature = string.Empty;
    private EventHandler? _sharedDatabasesChanged;
    private bool _watching;

    public SharedDatabaseChangeFeed(IConfigurationService configurationService,
        IDatabaseIdService databaseIdService,
        IDatabaseContextFactory contextFactory,
        DatabaseChangeMonitor monitor,
        ILogger<SharedDatabaseChangeFeed> logger)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _databaseIdService = databaseIdService ?? throw new ArgumentNullException(nameof(databaseIdService));
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public event EventHandler<SharedCollectionChangedEventArgs>? CollectionChanged
    {
        add
        {
            lock (_lock)
            {
                _collectionChanged += value;
                UpdateWatching();
            }
        }
        remove
        {
            lock (_lock)
            {
                _collectionChanged -= value;
                UpdateWatching();
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler? SharedDatabasesChanged
    {
        add
        {
            lock (_lock)
            {
                _sharedDatabasesChanged += value;
                UpdateWatching();
            }
        }
        remove
        {
            lock (_lock)
            {
                _sharedDatabasesChanged -= value;
                UpdateWatching();
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_lock)
        {
            _disposed = true;
            UpdateWatching();
        }
    }

    private void UpdateWatching()
    {
        var wanted = !_disposed && (_collectionChanged != null || _sharedDatabasesChanged != null);
        if (wanted == _watching)
            return;

        _watching = wanted;
        if (wanted)
        {
            _shareSignature = GetShareSignature();
            _monitor.DatabaseChanged += OnDatabaseChanged;
            _configurationService.Saved += OnConfigurationSaved;
        }
        else
        {
            _monitor.DatabaseChanged -= OnDatabaseChanged;
            _configurationService.Saved -= OnConfigurationSaved;
        }
    }

    private void OnConfigurationSaved(object? sender, EventArgs e)
    {
        EventHandler? handler;
        lock (_lock)
        {
            var signature = GetShareSignature();
            if (signature == _shareSignature)
                return;

            _shareSignature = signature;
            handler = _sharedDatabasesChanged;
        }

        handler?.Invoke(this, EventArgs.Empty);
    }

    private void OnDatabaseChanged(object? sender, DatabaseChangedEventArgs e)
    {
        // Only Shared Databases; the lookup is in memory, so a write to any other Database costs next to nothing
        var databaseKey = FindSharedDatabaseKey(e.DatabasePath);
        if (databaseKey == null)
            return;

        // Off the saving thread: reading the Database ID and the collections must not slow the write down
        _ = Task.Run(() => PublishAsync(databaseKey, e));
    }

    private async Task PublishAsync(string databaseKey, DatabaseChangedEventArgs change)
    {
        try
        {
            var databaseId = await _databaseIdService.GetDatabaseIdAsync(databaseKey);
            if (databaseId == null)
                return;

            var collectionIds = new HashSet<Guid>(change.CollectionIds);
            if (change.ClipIds.Count > 0)
            {
                var clips = _contextFactory.GetClipRepository(databaseKey);
                foreach (var item in change.ClipIds)
                {
                    var clip = await clips.GetByIdAsync(item);
                    if (clip?.CollectionId is { } collectionId)
                        collectionIds.Add(collectionId);

                    if (clip?.FolderId is { } folderId)
                        collectionIds.Add(folderId);
                }
            }

            // A Smart or SQL-based collection lists clips by its own SQL, so any clip change may change it
            if (change.ClipsChanged)
            {
                var collections = await _contextFactory.GetCollectionRepository(databaseKey).GetAllAsync();
                collectionIds.UnionWith(collections.Where(p => p.IsVirtual).Select(p => p.Id));
            }

            var handler = _collectionChanged;
            foreach (var item in collectionIds)
                handler?.Invoke(this, new SharedCollectionChangedEventArgs(new SharedCollectionChange(databaseId.Value, item)));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to report a change in Shared Database {DatabaseKey}", databaseKey);
        }
    }

    private string? FindSharedDatabaseKey(string databasePath)
    {
        foreach (var (key, configuration) in _configurationService.Configuration.Databases.ToList())
        {
            if (configuration.ShareWithPeers && IsSameFile(configuration, databasePath))
                return key;
        }

        return null;
    }

    private static bool IsSameFile(DatabaseConfiguration configuration, string databasePath)
    {
        if (string.IsNullOrWhiteSpace(configuration.FilePath))
            return false;

        try
        {
            var path = Path.GetFullPath(Environment.ExpandEnvironmentVariables(configuration.FilePath));
            return string.Equals(path, Path.GetFullPath(databasePath), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    /// <summary>
    /// What Peers see of the share list: which Databases are shared, under which names and from which files.
    /// </summary>
    private string GetShareSignature() =>
        string.Join('\n', _configurationService.Configuration.Databases.ToList()
            .Where(p => p.Value.ShareWithPeers)
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => $"{p.Key}|{p.Value.Name}|{p.Value.FilePath}"));
}
