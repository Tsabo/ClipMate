using System.Data.Common;
using ClipMate.Data.Schema.Abstractions;
using ClipMate.Data.Schema.Models;
using Microsoft.Data.Sqlite;

namespace ClipMate.Data.Schema.Sqlite;

/// <summary>
/// Reads schema from SQLite databases using sqlite_master and PRAGMA commands.
/// </summary>
public class SqliteSchemaReader : ISchemaReader
{
    private readonly DbConnection _connection;
    private readonly SchemaOptions _options;
    private Dictionary<string, SchemaDefinition>? _cache;

    public SqliteSchemaReader(DbConnection connection, SchemaOptions? options = null)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _options = options ?? new SchemaOptions();
    }

    public async Task<SchemaDefinition> ReadSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_options.EnableCaching && _cache != null && _cache.TryGetValue("schema", out var cached))
        {
            return cached;
        }

        var schema = new SchemaDefinition();

        if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync(cancellationToken);
        }

        var tables = await GetTablesAsync(cancellationToken);

        foreach (var tableName in tables)
        {
            if (_options.IgnoredTables.Contains(tableName))
                continue;

            var table = new TableDefinition { Name = tableName };
            table.Columns = await GetColumnsAsync(tableName, cancellationToken);
            table.Indexes = await GetIndexesAsync(tableName, cancellationToken);
            table.ForeignKeys = await GetForeignKeysAsync(tableName, cancellationToken);
            table.CreateSql = await GetCreateSqlAsync(tableName, cancellationToken);

            schema.Tables[tableName] = table;
        }

        if (_options.EnableCaching)
        {
            _cache ??= new Dictionary<string, SchemaDefinition>();
            _cache["schema"] = schema;
        }

        return schema;
    }

    private async Task<List<string>> GetTablesAsync(CancellationToken cancellationToken)
    {
        var tables = new List<string>();

        using var command = _connection.CreateCommand();
        command.CommandText = @"
            SELECT name 
            FROM sqlite_master 
            WHERE type='table' 
              AND name NOT LIKE 'sqlite_%'
            ORDER BY name";

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    private async Task<List<ColumnDefinition>> GetColumnsAsync(string tableName, CancellationToken cancellationToken)
    {
        var columns = new List<ColumnDefinition>();
        var ignoredColumns = _options.IgnoredColumns.GetValueOrDefault(tableName);

        using var command = _connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName})";

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var columnName = reader.GetString(1);
            
            if (ignoredColumns?.Contains(columnName) == true)
                continue;

            var isPrimaryKey = reader.GetInt32(5) > 0;
            var column = new ColumnDefinition
            {
                Position = reader.GetInt32(0),
                Name = columnName,
                Type = reader.GetString(2),
                // PRIMARY KEY columns are always NOT NULL in SQLite, even if PRAGMA shows notnull=0
                IsNullable = isPrimaryKey ? false : reader.GetInt32(3) == 0,
                IsPrimaryKey = isPrimaryKey,
                DefaultValue = reader.IsDBNull(4) ? null : reader.GetString(4)
            };

            columns.Add(column);
        }

        return columns;
    }
    private async Task<List<IndexDefinition>> GetIndexesAsync(string tableName, CancellationToken cancellationToken)
    {
        var indexes = new List<IndexDefinition>();

        using var listCommand = _connection.CreateCommand();
        listCommand.CommandText = $"PRAGMA index_list({tableName})";

        using var listReader = await listCommand.ExecuteReaderAsync(cancellationToken);
        while (await listReader.ReadAsync(cancellationToken))
        {
            var indexName = listReader.GetString(1);
            var isUnique = listReader.GetInt32(2) == 1;

            if (indexName.StartsWith("sqlite_autoindex_"))
                continue;

            var index = new IndexDefinition
            {
                Name = indexName,
                TableName = tableName,
                IsUnique = isUnique
            };

            using var infoCommand = _connection.CreateCommand();
            infoCommand.CommandText = $"PRAGMA index_info({indexName})";

            using var infoReader = await infoCommand.ExecuteReaderAsync(cancellationToken);
            while (await infoReader.ReadAsync(cancellationToken))
            {
                var columnName = infoReader.GetString(2);
                index.Columns.Add(columnName);
            }

            using var sqlCommand = _connection.CreateCommand();
            sqlCommand.CommandText = @"
                SELECT sql 
                FROM sqlite_master 
                WHERE type='index' AND name=@indexName";

            var param = sqlCommand.CreateParameter();
            param.ParameterName = "@indexName";
            param.Value = indexName;
            sqlCommand.Parameters.Add(param);

            var sql = await sqlCommand.ExecuteScalarAsync(cancellationToken);
            index.CreateSql = sql?.ToString();

            indexes.Add(index);
        }

        return indexes;
    }

    private async Task<List<ForeignKeyDefinition>> GetForeignKeysAsync(string tableName, CancellationToken cancellationToken)
    {
        var foreignKeys = new List<ForeignKeyDefinition>();

        using var command = _connection.CreateCommand();
        command.CommandText = $"PRAGMA foreign_key_list({tableName})";

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var fk = new ForeignKeyDefinition
            {
                ColumnName = reader.GetString(3),
                ReferencedTable = reader.GetString(2),
                ReferencedColumn = reader.GetString(4),
                OnDelete = reader.IsDBNull(6) ? null : reader.GetString(6),
                OnUpdate = reader.IsDBNull(5) ? null : reader.GetString(5)
            };

            foreignKeys.Add(fk);
        }

        return foreignKeys;
    }

    private async Task<string?> GetCreateSqlAsync(string tableName, CancellationToken cancellationToken)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = @"
            SELECT sql 
            FROM sqlite_master 
            WHERE type='table' AND name=@tableName";

        var param = command.CreateParameter();
        param.ParameterName = "@tableName";
        param.Value = tableName;
        command.Parameters.Add(param);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result?.ToString();
    }
}
