using System.Collections.Concurrent;
using ClipMate.Core.Services.Abstractions;
using Microsoft.Data.Sqlite;

namespace ClipMate.Data.Services;

/// <summary>
/// Reads the Database ID straight from the database file without creating it, and replaces it on request.
/// </summary>
/// <remarks>
/// A configured Database's ID is remembered per database key and file path, so serving Peers does not reopen every
/// Shared Database's file on every call. The remembered IDs are forgotten whenever the configuration is saved (a
/// Database added, moved or removed) and whenever this service gives a file a new Database ID. "No Database ID yet" is
/// never remembered, because opening the Database gives it one.
/// </remarks>
internal sealed class DatabaseIdService : IDatabaseIdService
{
    private readonly ConcurrentDictionary<(string DatabaseKey, string FilePath), Guid> _cache = new();
    private readonly IConfigurationService _configurationService;
    private int _generation;

    public DatabaseIdService(IConfigurationService configurationService)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));

        // A singleton, like the configuration service, so the subscription lasts as long as both
        _configurationService.Saved += OnConfigurationSaved;
    }

    /// <inheritdoc />
    public async Task<Guid?> GetDatabaseIdAsync(string databaseKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseKey);

        if (!_configurationService.Configuration.Databases.TryGetValue(databaseKey, out var databaseConfig) ||
            string.IsNullOrWhiteSpace(databaseConfig.FilePath))
            return null;

        var key = (databaseKey, databaseConfig.FilePath);
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        // Forgetting while the file was being read must win over remembering what was read
        var generation = Volatile.Read(ref _generation);
        var databaseId = await GetDatabaseIdForFileAsync(databaseConfig.FilePath, cancellationToken);
        if (databaseId is { } id && generation == Volatile.Read(ref _generation))
            _cache[key] = id;

        return databaseId;
    }

    /// <inheritdoc />
    public async Task<Guid?> GetDatabaseIdForFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var path = Environment.ExpandEnvironmentVariables(filePath);
        if (!File.Exists(path))
            return null;

        // Read-only and unpooled so reading the ID never creates the file or keeps it locked
        await using var connection = new SqliteConnection(CreateConnectionString(path, SqliteOpenMode.ReadOnly));

        try
        {
            await connection.OpenAsync(cancellationToken);

            return await DatabaseIdStore.ReadDatabaseIdAsync(connection, cancellationToken);
        }
        catch (SqliteException)
        {
            // Not a readable ClipMate database, or the DatabaseInfo table does not exist until the Database is
            // opened by this version
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<Guid> AssignNewDatabaseIdAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var path = Environment.ExpandEnvironmentVariables(filePath);
        if (!File.Exists(path))
            throw new FileNotFoundException("The database file does not exist.", path);

        try
        {
            // ReadWrite (not ReadWriteCreate), so a file deleted meanwhile is never recreated empty
            await using var connection = new SqliteConnection(CreateConnectionString(path, SqliteOpenMode.ReadWrite));
            await connection.OpenAsync(cancellationToken);

            return await DatabaseIdStore.ReplaceDatabaseIdAsync(connection, cancellationToken);
        }
        finally
        {
            Forget();
        }
    }

    private void OnConfigurationSaved(object? sender, EventArgs e) => Forget();

    private void Forget()
    {
        Interlocked.Increment(ref _generation);
        _cache.Clear();
    }

    private static string CreateConnectionString(string path, SqliteOpenMode mode) => new SqliteConnectionStringBuilder
    {
        DataSource = path,
        Mode = mode,
        Pooling = false,
    }.ToString();
}
