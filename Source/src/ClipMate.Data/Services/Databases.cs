using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services.Abstractions;

namespace ClipMate.Data.Services;

/// <summary>
/// <see cref="IDatabases" /> over the configured Databases and the Remote Databases of <see cref="IRemoteDatabaseService" />.
/// The only place a database key is read to tell a local Database from a Remote one.
/// </summary>
internal sealed class Databases : IDatabases, IDisposable
{
    private readonly IConfigurationService _configurationService;
    private readonly IDatabaseContextFactory _databaseContextFactory;
    private readonly IRemoteDatabaseService _remoteDatabases;
    private readonly ISearchService _searchService;

    public Databases(IConfigurationService configurationService,
        IRemoteDatabaseService remoteDatabases,
        IDatabaseContextFactory databaseContextFactory,
        ISearchService searchService)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _remoteDatabases = remoteDatabases ?? throw new ArgumentNullException(nameof(remoteDatabases));
        _databaseContextFactory = databaseContextFactory ?? throw new ArgumentNullException(nameof(databaseContextFactory));
        _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));

        _configurationService.Saved += OnChanged;
        _remoteDatabases.RemoteDatabasesChanged += OnChanged;
    }

    /// <inheritdoc />
    public IReadOnlyList<IDatabase> All =>
    [
        .. _configurationService.Configuration.Databases.Select(p => (IDatabase)CreateLocal(p.Key, p.Value)),
        .. _remoteDatabases.GetRemoteDatabases().Select(_remoteDatabases.Open),
    ];

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public IDatabase Get(string databaseKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseKey);

        if (RemoteDatabaseKey.TryParse(databaseKey, out var address))
        {
            var remote = _remoteDatabases.GetRemoteDatabases().FirstOrDefault(p => p.Address == address)
                         ?? throw new DatabaseUnavailableException("That Remote Database is no longer shared.");

            return _remoteDatabases.Open(remote);
        }

        return _configurationService.Configuration.Databases.TryGetValue(databaseKey, out var configuration)
            ? CreateLocal(databaseKey, configuration)
            : throw new DatabaseUnavailableException($"Database '{databaseKey}' is no longer configured.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _configurationService.Saved -= OnChanged;
        _remoteDatabases.RemoteDatabasesChanged -= OnChanged;
    }

    private LocalDatabase CreateLocal(string key, DatabaseConfiguration configuration) =>
        new(key, configuration, _databaseContextFactory, _searchService);

    private void OnChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
}
