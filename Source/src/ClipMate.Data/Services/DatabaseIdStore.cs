using System.Data.Common;
using System.Globalization;
using ClipMate.Core.Models;

namespace ClipMate.Data.Services;

/// <summary>
/// Reads and assigns the permanent Database ID kept in the single-row <c>DatabaseInfo</c> table.
/// </summary>
internal static class DatabaseIdStore
{
    /// <summary>
    /// Assigns a new Database ID if the database does not have one yet. An existing ID is never changed.
    /// The <c>DatabaseInfo</c> table must already exist (created by EnsureCreated or schema migration).
    /// </summary>
    public static async Task EnsureDatabaseIdAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT OR IGNORE INTO DatabaseInfo (Id, DatabaseId) VALUES ($id, $databaseId)";
        AddParameter(command, "$id", DatabaseInfo.SingletonId);

        // Same text format EF Core uses for Guid columns on SQLite
        AddParameter(command, "$databaseId", Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture).ToUpperInvariant());

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Reads the Database ID, or returns null if the database has none yet.
    /// </summary>
    public static async Task<Guid?> ReadDatabaseIdAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT DatabaseId FROM DatabaseInfo WHERE Id = $id";
        AddParameter(command, "$id", DatabaseInfo.SingletonId);

        var value = await command.ExecuteScalarAsync(cancellationToken);

        return value is string text && Guid.TryParse(text, out var databaseId)
            ? databaseId
            : null;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
