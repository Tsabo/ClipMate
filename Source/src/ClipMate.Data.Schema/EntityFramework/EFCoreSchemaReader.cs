using ClipMate.Data.Schema.Abstractions;
using ClipMate.Data.Schema.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ClipMate.Data.Schema.EntityFramework;

/// <summary>
/// Reads schema from EF Core model metadata.
/// </summary>
public class EFCoreSchemaReader : ISchemaReader
{
    private readonly IModel _model;
    private readonly SchemaOptions _options;
    private SchemaDefinition? _cache;

    public EFCoreSchemaReader(IModel model, SchemaOptions? options = null)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _options = options ?? new SchemaOptions();
    }

    public Task<SchemaDefinition> ReadSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_options.EnableCaching && _cache != null)
            return Task.FromResult(_cache);

        var schema = new SchemaDefinition();

        foreach (var entityType in _model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (string.IsNullOrEmpty(tableName) || _options.IgnoredTables.Contains(tableName))
                continue;

            var table = new TableDefinition { Name = tableName };

            var position = 0;
            foreach (var property in entityType.GetProperties())
            {
                var columnName = property.GetColumnName();

                if (_options.IgnoredColumns.TryGetValue(tableName, out var ignoredColumns) &&
                    ignoredColumns.Contains(columnName))
                    continue;

                var column = new ColumnDefinition
                {
                    Name = columnName,
                    Type = MapToSqliteType(property.GetColumnType()),
                    IsNullable = property.IsNullable,
                    IsPrimaryKey = property.IsPrimaryKey(),
                    DefaultValue = property.GetDefaultValueSql(),
                    Position = position++,
                };

                table.Columns.Add(column);
            }

            foreach (var index in entityType.GetIndexes())
            {
                var indexName = index.GetDatabaseName();
                if (string.IsNullOrEmpty(indexName))
                    continue;

                var indexDef = new IndexDefinition
                {
                    Name = indexName,
                    TableName = tableName,
                    IsUnique = index.IsUnique,
                    Columns = index.Properties.Select(p => p.GetColumnName()).ToList(),
                };

                table.Indexes.Add(indexDef);
            }

            foreach (var foreignKey in entityType.GetForeignKeys())
            {
                var principalTable = foreignKey.PrincipalEntityType.GetTableName();
                if (string.IsNullOrEmpty(principalTable))
                    continue;

                foreach (var property in foreignKey.Properties)
                {
                    var principalProperty = foreignKey.PrincipalKey.Properties.First();

                    var fkDef = new ForeignKeyDefinition
                    {
                        ColumnName = property.GetColumnName(),
                        ReferencedTable = principalTable,
                        ReferencedColumn = principalProperty.GetColumnName(),
                        OnDelete = MapDeleteBehavior(foreignKey.DeleteBehavior),
                        OnUpdate = null,
                    };

                    table.ForeignKeys.Add(fkDef);
                }
            }

            schema.Tables[tableName] = table;
        }

        if (_options.EnableCaching)
            _cache = schema;

        return Task.FromResult(schema);
    }

    private static string MapToSqliteType(string efCoreType)
    {
        var type = efCoreType.ToUpperInvariant();

        if (type.Contains("INT") || type.Contains("BOOL"))
            return "INTEGER";

        if (type.Contains("TEXT") || type.Contains("VARCHAR") || type.Contains("CHAR"))
            return "TEXT";

        if (type.Contains("REAL") || type.Contains("FLOAT") || type.Contains("DOUBLE"))
            return "REAL";

        if (type.Contains("BLOB"))
            return "BLOB";

        return "TEXT";
    }

    private static string MapDeleteBehavior(DeleteBehavior deleteBehavior)
    {
        return deleteBehavior switch
        {
            DeleteBehavior.Cascade => "CASCADE",
            DeleteBehavior.SetNull => "SET NULL",
            DeleteBehavior.Restrict => "RESTRICT",
            var _ => "NO ACTION",
        };
    }
}
