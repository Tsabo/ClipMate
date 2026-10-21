using ClipMate.Core.Services;
using Microsoft.Data.Sqlite;

namespace ClipMate.Data.Services;

/// <summary>
/// Reads the Database ID straight from the database file without creating or modifying it.
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

        var path = Environment.ExpandEnvironmentVariables(databaseConfig.FilePath);
        if (!File.Exists(path))
            return null;

        // Read-only and unpooled so reading the ID never creates the file or keeps it locked
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString();

        await using var connection = new SqliteConnection(connectionString);

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
}
