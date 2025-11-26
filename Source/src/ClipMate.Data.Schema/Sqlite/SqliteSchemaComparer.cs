using System.Text;
using ClipMate.Data.Schema.Abstractions;
using ClipMate.Data.Schema.Models;

namespace ClipMate.Data.Schema.Sqlite;

/// <summary>
/// Compares schemas and generates SQLite-compatible migration operations.
/// </summary>
public class SqliteSchemaComparer : ISchemaComparer
{
    public SchemaDiff Compare(SchemaDefinition current, SchemaDefinition expected)
    {
        var diff = new SchemaDiff();

        // Check for removed tables
        foreach (var item in current.Tables.Values)
        {
            if (!expected.Tables.ContainsKey(item.Name))
                diff.Warnings.Add($"Table '{item.Name}' exists in current schema but not in expected schema (will be removed)");
        }

        foreach (var item in expected.Tables.Values)
        {
            if (!current.Tables.TryGetValue(item.Name, out var currentTable))
            {
                diff.Operations.Add(new MigrationOperation
                {
                    Type = MigrationOperationType.CreateTable,
                    TableName = item.Name,
                    Sql = GenerateCreateTableSql(item),
                });
            }
            else
            {
                CompareColumns(currentTable, item, diff);
                CompareIndexes(currentTable, item, diff);
            }
        }

        return diff;
    }

    private void CompareColumns(TableDefinition current, TableDefinition expected, SchemaDiff diff)
    {
        var currentColumnNames = current.Columns.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var currentColumnsDict = current.Columns.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        var expectedColumnNames = expected.Columns.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Check for removed columns
        foreach (var item in current.Columns)
        {
            if (!expectedColumnNames.Contains(item.Name))
                diff.Warnings.Add($"Column '{item.Name}' in table '{expected.Name}' exists in current schema but not in expected schema (will be removed)");
        }

        foreach (var item in expected.Columns)
        {
            if (!currentColumnNames.Contains(item.Name))
            {
                diff.Operations.Add(new MigrationOperation
                {
                    Type = MigrationOperationType.AddColumn,
                    TableName = expected.Name,
                    ColumnName = item.Name,
                    Sql = GenerateAddColumnSql(expected.Name, item),
                });
            }
            else if (currentColumnsDict.TryGetValue(item.Name, out var currentColumn))
            {
                // Check for type changes
                if (!string.Equals(currentColumn.Type, item.Type, StringComparison.OrdinalIgnoreCase))
                    diff.Warnings.Add($"Column '{item.Name}' in table '{expected.Name}' has type change from '{currentColumn.Type}' to '{item.Type}'");
            }
        }
    }

    private void CompareIndexes(TableDefinition current, TableDefinition expected, SchemaDiff diff)
    {
        var currentIndexNames = current.Indexes.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var item in expected.Indexes)
        {
            if (!currentIndexNames.Contains(item.Name))
            {
                diff.Operations.Add(new MigrationOperation
                {
                    Type = MigrationOperationType.CreateIndex,
                    TableName = expected.Name,
                    IndexName = item.Name,
                    Sql = GenerateCreateIndexSql(item),
                });
            }
        }
    }

    private static string GenerateCreateTableSql(TableDefinition table)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"CREATE TABLE {table.Name} (");

        var columnDefs = new List<string>();
        foreach (var item in table.Columns)
        {
            var def = $"    {item.Name} {item.Type}";

            if (item.IsPrimaryKey)
                def += " PRIMARY KEY AUTOINCREMENT";

            if (!item.IsNullable && !item.IsPrimaryKey)
                def += " NOT NULL";

            if (item.DefaultValue != null)
                def += $" DEFAULT {item.DefaultValue}";

            columnDefs.Add(def);
        }

        foreach (var item in table.ForeignKeys)
        {
            var fkDef = $"    FOREIGN KEY ({item.ColumnName}) REFERENCES {item.ReferencedTable}({item.ReferencedColumn})";

            if (item.OnDelete != null)
                fkDef += $" ON DELETE {ConvertDeleteBehavior(item.OnDelete)}";

            columnDefs.Add(fkDef);
        }

        sb.AppendLine(string.Join(",\n", columnDefs));
        sb.Append(")");

        return sb.ToString();
    }

    private static string GenerateAddColumnSql(string tableName, ColumnDefinition column)
    {
        var sql = $"ALTER TABLE {tableName} ADD COLUMN {column.Name} {column.Type}";

        if (!column.IsNullable)
            sql += " NOT NULL";

        if (column.DefaultValue != null)
            sql += $" DEFAULT {column.DefaultValue}";
        else if (!column.IsNullable)
            sql += " DEFAULT " + GetDefaultValueForType(column.Type);

        return sql;
    }

    private static string GenerateCreateIndexSql(IndexDefinition index)
    {
        var unique = index.IsUnique
            ? "UNIQUE "
            : "";

        var columns = string.Join(", ", index.Columns);
        return $"CREATE {unique}INDEX {index.Name} ON {index.TableName} ({columns})";
    }

    private static string GetDefaultValueForType(string sqliteType)
    {
        var type = sqliteType.ToUpperInvariant();

        if (type.Contains("INT"))
            return "0";

        if (type.Contains("REAL") || type.Contains("FLOAT") || type.Contains("DOUBLE"))
            return "0.0";

        if (type.Contains("TEXT"))
            return "''";

        if (type.Contains("BLOB"))
            return "x''";

        return "NULL";
    }

    private static string ConvertDeleteBehavior(string deleteBehavior)
    {
        return deleteBehavior.ToUpperInvariant() switch
        {
            "CASCADE" => "CASCADE",
            "SETNULL" => "SET NULL",
            "SETDEFAULT" => "SET DEFAULT",
            "RESTRICT" => "RESTRICT",
            var _ => "NO ACTION",
        };
    }
}
