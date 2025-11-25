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
        foreach (var currentTable in current.Tables.Values)
        {
            if (!expected.Tables.ContainsKey(currentTable.Name))
            {
                diff.Warnings.Add($"Table '{currentTable.Name}' exists in current schema but not in expected schema (will be removed)");
            }
        }

        foreach (var expectedTable in expected.Tables.Values)
        {
            if (!current.Tables.ContainsKey(expectedTable.Name))
            {
                diff.Operations.Add(new MigrationOperation
                {
                    Type = MigrationOperationType.CreateTable,
                    TableName = expectedTable.Name,
                    Sql = GenerateCreateTableSql(expectedTable)
                });
            }
            else
            {
                var currentTable = current.Tables[expectedTable.Name];
                CompareColumns(currentTable, expectedTable, diff);
                CompareIndexes(currentTable, expectedTable, diff);
            }
        }

        return diff;
    }

    private void CompareColumns(TableDefinition current, TableDefinition expected, SchemaDiff diff)
    {
        var currentColumnNames = current.Columns.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var currentColumnsDict = current.Columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        var expectedColumnNames = expected.Columns.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Check for removed columns
        foreach (var currentColumn in current.Columns)
        {
            if (!expectedColumnNames.Contains(currentColumn.Name))
            {
                diff.Warnings.Add($"Column '{currentColumn.Name}' in table '{expected.Name}' exists in current schema but not in expected schema (will be removed)");
            }
        }

        foreach (var expectedColumn in expected.Columns)
        {
            if (!currentColumnNames.Contains(expectedColumn.Name))
            {
                diff.Operations.Add(new MigrationOperation
                {
                    Type = MigrationOperationType.AddColumn,
                    TableName = expected.Name,
                    ColumnName = expectedColumn.Name,
                    Sql = GenerateAddColumnSql(expected.Name, expectedColumn)
                });
            }
            else if (currentColumnsDict.TryGetValue(expectedColumn.Name, out var currentColumn))
            {
                // Check for type changes
                if (!string.Equals(currentColumn.Type, expectedColumn.Type, StringComparison.OrdinalIgnoreCase))
                {
                    diff.Warnings.Add($"Column '{expectedColumn.Name}' in table '{expected.Name}' has type change from '{currentColumn.Type}' to '{expectedColumn.Type}'");
                }
            }
        }
    }

    private void CompareIndexes(TableDefinition current, TableDefinition expected, SchemaDiff diff)
    {
        var currentIndexNames = current.Indexes.Select(i => i.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var expectedIndex in expected.Indexes)
        {
            if (!currentIndexNames.Contains(expectedIndex.Name))
            {
                diff.Operations.Add(new MigrationOperation
                {
                    Type = MigrationOperationType.CreateIndex,
                    TableName = expected.Name,
                    IndexName = expectedIndex.Name,
                    Sql = GenerateCreateIndexSql(expectedIndex)
                });
            }
        }
    }

    private static string GenerateCreateTableSql(TableDefinition table)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"CREATE TABLE {table.Name} (");

        var columnDefs = new List<string>();
        foreach (var column in table.Columns)
        {
            var def = $"    {column.Name} {column.Type}";

            if (column.IsPrimaryKey)
                def += " PRIMARY KEY AUTOINCREMENT";

            if (!column.IsNullable && !column.IsPrimaryKey)
                def += " NOT NULL";

            if (column.DefaultValue != null)
                def += $" DEFAULT {column.DefaultValue}";

            columnDefs.Add(def);
        }

        foreach (var fk in table.ForeignKeys)
        {
            var fkDef = $"    FOREIGN KEY ({fk.ColumnName}) REFERENCES {fk.ReferencedTable}({fk.ReferencedColumn})";

            if (fk.OnDelete != null)
                fkDef += $" ON DELETE {ConvertDeleteBehavior(fk.OnDelete)}";

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
        var unique = index.IsUnique ? "UNIQUE " : "";
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
            _ => "NO ACTION"
        };
    }
}
