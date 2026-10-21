using ClipMate.Core.Services;
using Microsoft.Data.Sqlite;

namespace ClipMate.Data.Services;

/// <summary>
/// Reads the Database ID straight from the database file without creating it, and replaces it on request.
/// </summary>
internal class DatabaseIdService : IDatabaseIdService
{
    private readonly IConfigurationService _configurationService;

    public DatabaseIdService(IConfigurationService configurationService)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
    }

    /// <inheritdoc />
    public async Task<Guid?> GetDatabaseIdAsync(string databaseKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseKey);

        if (!_configurationService.Configuration.Databases.TryGetValue(databaseKey, out var databaseConfig))
            return null;

        return await GetDatabaseIdForFileAsync(databaseConfig.FilePath, cancellationToken);
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

        // ReadWrite (not ReadWriteCreate), so a file deleted meanwhile is never recreated empty
        await using var connection = new SqliteConnection(CreateConnectionString(path, SqliteOpenMode.ReadWrite));
        await connection.OpenAsync(cancellationToken);

        return await DatabaseIdStore.ReplaceDatabaseIdAsync(connection, cancellationToken);
    }

    private static string CreateConnectionString(string path, SqliteOpenMode mode) => new SqliteConnectionStringBuilder
    {
        DataSource = path,
        Mode = mode,
        Pooling = false,
    }.ToString();
}
