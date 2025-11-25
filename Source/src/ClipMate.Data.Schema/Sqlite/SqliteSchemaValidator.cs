using ClipMate.Data.Schema.Abstractions;
using ClipMate.Data.Schema.Models;

namespace ClipMate.Data.Schema.Sqlite;

/// <summary>
/// Validates SQLite schema definitions.
/// </summary>
public class SqliteSchemaValidator : ISchemaValidator
{
    public ValidationResult Validate(SchemaDefinition schema)
    {
        var result = new ValidationResult();

        ValidateTableNames(schema, result);
        ValidateColumnNames(schema, result);
        ValidateCircularForeignKeys(schema, result);
        ValidateForeignKeyReferences(schema, result);
        ValidateColumnTypes(schema, result);
        ValidateDuplicateNames(schema, result);
        ValidatePrimaryKeys(schema, result);

        return result;
    }

    private void ValidateTableNames(SchemaDefinition schema, ValidationResult result)
    {
        foreach (var table in schema.Tables.Values)
        {
            if (string.IsNullOrWhiteSpace(table.Name))
            {
                result.Errors.Add("Table name cannot be empty");
            }
            else if (table.Name.StartsWith("sqlite_", StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add($"Table name '{table.Name}' is reserved by SQLite");
            }
            else if (!IsValidIdentifier(table.Name))
            {
                result.Errors.Add($"Invalid table name '{table.Name}': must start with a letter or underscore");
            }
        }
    }

    private void ValidateColumnNames(SchemaDefinition schema, ValidationResult result)
    {
        foreach (var table in schema.Tables.Values)
        {
            foreach (var column in table.Columns)
            {
                if (string.IsNullOrWhiteSpace(column.Name))
                {
                    result.Errors.Add($"Column name cannot be empty in table '{table.Name}'");
                }
                else if (!IsValidIdentifier(column.Name))
                {
                    result.Errors.Add($"Invalid column name '{column.Name}' in table '{table.Name}': must start with a letter or underscore");
                }
            }
        }
    }

    private static bool IsValidIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        var firstChar = name[0];
        return char.IsLetter(firstChar) || firstChar == '_';
    }

    private void ValidateForeignKeyReferences(SchemaDefinition schema, ValidationResult result)
    {
        foreach (var table in schema.Tables.Values)
        {
            foreach (var fk in table.ForeignKeys)
            {
                if (!schema.Tables.ContainsKey(fk.ReferencedTable))
                {
                    result.Errors.Add($"Foreign key in table '{table.Name}' references table '{fk.ReferencedTable}' which does not exist");
                }
            }
        }
    }

    private void ValidateCircularForeignKeys(SchemaDefinition schema, ValidationResult result)
    {
        var graph = BuildForeignKeyGraph(schema);

        foreach (var tableName in graph.Keys)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (HasCircularReference(tableName, tableName, graph, visited, isStart: true))
            {
                result.Errors.Add($"Detected circular foreign key reference involving table '{tableName}'");
            }
        }
    }

    private Dictionary<string, List<string>> BuildForeignKeyGraph(SchemaDefinition schema)
    {
        var graph = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var table in schema.Tables.Values)
        {
            if (!graph.ContainsKey(table.Name))
                graph[table.Name] = new List<string>();

            foreach (var fk in table.ForeignKeys)
            {
                graph[table.Name].Add(fk.ReferencedTable);
            }
        }

        return graph;
    }

    private bool HasCircularReference(
        string startTable,
        string currentTable,
        Dictionary<string, List<string>> graph,
        HashSet<string> visited,
        bool isStart = false)
    {
        if (!graph.TryGetValue(currentTable, out var references))
            return false;

        foreach (var refTable in references)
        {
            // Allow self-references (e.g., Collections.ParentId → Collections.Id)
            // But detect true circular references between different tables
            if (refTable.Equals(startTable, StringComparison.OrdinalIgnoreCase))
            {
                // If this is the starting table checking its own references, skip self-references
                if (isStart && refTable.Equals(currentTable, StringComparison.OrdinalIgnoreCase))
                    continue;
                
                // If we've visited other tables and came back to start, it's circular
                if (visited.Count > 0)
                    return true;
            }

            if (visited.Add(refTable))
            {
                if (HasCircularReference(startTable, refTable, graph, visited, isStart: false))
                    return true;

                visited.Remove(refTable);
            }
        }

        return false;
    }

    private void ValidateColumnTypes(SchemaDefinition schema, ValidationResult result)
    {
        var validTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "INTEGER", "INT", "TINYINT", "SMALLINT", "MEDIUMINT", "BIGINT",
            "UNSIGNED BIG INT", "INT2", "INT8",
            "TEXT", "CLOB", "CHARACTER", "VARCHAR", "VARYING CHARACTER",
            "NCHAR", "NATIVE CHARACTER", "NVARCHAR",
            "REAL", "DOUBLE", "DOUBLE PRECISION", "FLOAT",
            "NUMERIC", "DECIMAL", "BOOLEAN", "DATE", "DATETIME",
            "BLOB"
        };

        foreach (var table in schema.Tables.Values)
        {
            foreach (var column in table.Columns)
            {
                if (string.IsNullOrWhiteSpace(column.Type))
                {
                    result.Errors.Add($"Column '{column.Name}' in table '{table.Name}' has no type specified");
                    continue;
                }

                var baseType = column.Type.Split('(')[0].Trim().ToUpperInvariant();

                if (!validTypes.Contains(baseType))
                {
                    result.Errors.Add($"Column '{column.Name}' in table '{table.Name}' has invalid type '{column.Type}'");
                }
            }
        }
    }

    private void ValidateDuplicateNames(SchemaDefinition schema, ValidationResult result)
    {
        foreach (var table in schema.Tables.Values)
        {
            var columnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in table.Columns)
            {
                if (!columnNames.Add(column.Name))
                {
                    result.Errors.Add($"Table '{table.Name}' has duplicate column name '{column.Name}'");
                }
            }

            var indexNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var index in table.Indexes)
            {
                if (!indexNames.Add(index.Name))
                {
                    result.Errors.Add($"Table '{table.Name}' has duplicate index name '{index.Name}'");
                }
            }
        }
    }

    private void ValidatePrimaryKeys(SchemaDefinition schema, ValidationResult result)
    {
        foreach (var table in schema.Tables.Values)
        {
            var hasPrimaryKey = table.Columns.Any(c => c.IsPrimaryKey);
            if (!hasPrimaryKey)
            {
                result.Warnings.Add($"Table '{table.Name}' does not have a primary key");
            }
        }
    }
}
