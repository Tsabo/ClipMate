using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ClipMate.Data.Services;

/// <summary>
/// Provides SQL schema information for Monaco Editor IntelliSense.
/// Extracts table and column metadata from EF Core DbContext.
/// </summary>
public static class SqlSchemaProvider
{
    private static volatile SqlSchema? _cachedSchema;
    private static readonly Lock _lock = new();

    /// <summary>
    /// Gets the SQL schema including tables, columns, functions, and keywords.
    /// Schema is cached after first extraction.
    /// </summary>
    public static SqlSchema GetSchema(DbContext dbContext)
    {
        if (_cachedSchema != null)
            return _cachedSchema;

        lock (_lock)
        {
            if (_cachedSchema != null)
                return _cachedSchema;

            var schema = ExtractSchema(dbContext);
            _cachedSchema = schema;
            return schema;
        }
    }

    private static SqlSchema ExtractSchema(DbContext dbContext)
    {
        // Only include commonly-queried tables for clip searches
        var includedTables = new HashSet<string>
        {
            "Clips", "Collections", "ClipData",
            "BlobTxt", "BlobJpg", "BlobPng", "BlobBlob",
        };

        var tables = new List<TableSchema>();

        foreach (var item in dbContext.Model.GetEntityTypes())
        {
            var tableName = item.GetTableName();
            if (tableName == null || !includedTables.Contains(tableName))
                continue;

            var columns = item.GetProperties()
                .Select(p => new ColumnSchema
                {
                    Name = p.GetColumnName(),
                    Type = GetSqliteType(p),
                    IsNullable = p.IsNullable,
                    IsPrimaryKey = p.IsPrimaryKey(),
                })
                .ToList();

            tables.Add(new TableSchema
            {
                Name = tableName,
                Columns = columns,
            });
        }

        return new SqlSchema
        {
            Tables = tables,
            Functions = GetSqliteFunctions(),
            Keywords = GetSqlKeywords(),
        };
    }

    private static string GetSqliteType(IProperty property)
    {
        var clrType = property.ClrType;
        var underlyingType = Nullable.GetUnderlyingType(clrType) ?? clrType;

        return underlyingType.Name switch
        {
            nameof(Int32) => "INTEGER",
            nameof(Int64) => "INTEGER",
            nameof(Boolean) => "INTEGER",
            nameof(Byte) => "INTEGER",
            nameof(Double) => "REAL",
            nameof(Single) => "REAL",
            nameof(Decimal) => "REAL",
            nameof(String) => "TEXT",
            nameof(Guid) => "TEXT",
            nameof(DateTime) => "TEXT",
            nameof(DateTimeOffset) => "TEXT",
            var _ when underlyingType == typeof(byte[]) => "BLOB",
            var _ => "TEXT",
        };
    }

    private static List<FunctionSchema> GetSqliteFunctions()
    {
        return
        [
            new() { Name = "TextSearch", Description = "ClipMate text search with wildcards and OR logic", Category = "Custom" },

            // Date/Time Functions
            new() { Name = "DATE", Description = "Returns date as YYYY-MM-DD", Category = "Date/Time" },
            new() { Name = "TIME", Description = "Returns time as HH:MM:SS", Category = "Date/Time" },
            new() { Name = "DATETIME", Description = "Returns datetime as YYYY-MM-DD HH:MM:SS", Category = "Date/Time" },
            new() { Name = "JULIANDAY", Description = "Returns Julian day number", Category = "Date/Time" },
            new() { Name = "STRFTIME", Description = "Format date/time string", Category = "Date/Time" },
            new() { Name = "UNIXEPOCH", Description = "Returns Unix timestamp", Category = "Date/Time" },

            // String Functions
            new() { Name = "SUBSTR", Description = "Extract substring", Category = "String" },
            new() { Name = "SUBSTRING", Description = "Extract substring (alias)", Category = "String" },
            new() { Name = "LENGTH", Description = "String length in characters", Category = "String" },
            new() { Name = "TRIM", Description = "Remove leading/trailing spaces", Category = "String" },
            new() { Name = "LTRIM", Description = "Remove leading spaces", Category = "String" },
            new() { Name = "RTRIM", Description = "Remove trailing spaces", Category = "String" },
            new() { Name = "UPPER", Description = "Convert to uppercase", Category = "String" },
            new() { Name = "LOWER", Description = "Convert to lowercase", Category = "String" },
            new() { Name = "REPLACE", Description = "Replace substring", Category = "String" },
            new() { Name = "INSTR", Description = "Find substring position", Category = "String" },
            new() { Name = "CONCAT", Description = "Concatenate strings", Category = "String" },
            new() { Name = "CONCAT_WS", Description = "Concatenate with separator", Category = "String" },
            new() { Name = "CHAR", Description = "Character from code", Category = "String" },

            // Aggregate Functions
            new() { Name = "COUNT", Description = "Count rows", Category = "Aggregate" },
            new() { Name = "SUM", Description = "Sum numeric values", Category = "Aggregate" },
            new() { Name = "AVG", Description = "Average value", Category = "Aggregate" },
            new() { Name = "MAX", Description = "Maximum value", Category = "Aggregate" },
            new() { Name = "MIN", Description = "Minimum value", Category = "Aggregate" },
            new() { Name = "GROUP_CONCAT", Description = "Concatenate values with separator", Category = "Aggregate" },
            new() { Name = "TOTAL", Description = "Sum returning 0.0 for empty set", Category = "Aggregate" },

            // Type/Conversion Functions
            new() { Name = "CAST", Description = "Convert between types", Category = "Type" },
            new() { Name = "TYPEOF", Description = "Return type name", Category = "Type" },
            new() { Name = "COALESCE", Description = "Return first non-NULL value", Category = "Type" },
            new() { Name = "IFNULL", Description = "Replace NULL with value", Category = "Type" },
            new() { Name = "NULLIF", Description = "Return NULL if equal", Category = "Type" },

            // Math Functions
            new() { Name = "ABS", Description = "Absolute value", Category = "Math" },
            new() { Name = "ROUND", Description = "Round number", Category = "Math" },
            new() { Name = "CEIL", Description = "Round up", Category = "Math" },
            new() { Name = "CEILING", Description = "Round up (alias)", Category = "Math" },
            new() { Name = "FLOOR", Description = "Round down", Category = "Math" },
            new() { Name = "RANDOM", Description = "Random integer", Category = "Math" },
            new() { Name = "SIGN", Description = "Sign of number", Category = "Math" },
            new() { Name = "SQRT", Description = "Square root", Category = "Math" },
            new() { Name = "MOD", Description = "Modulo operation", Category = "Math" },

            // Other Functions
            new() { Name = "IIF", Description = "Inline IF statement", Category = "Other" },
            new() { Name = "HEX", Description = "Convert to hexadecimal", Category = "Other" },
            new() { Name = "QUOTE", Description = "Quote value for SQL", Category = "Other" },
            new() { Name = "ZEROBLOB", Description = "Create blob of zeros", Category = "Other" },
        ];
    }

    private static List<string> GetSqlKeywords()
    {
        return
        [
            "SELECT", "DISTINCT", "ALL", "FROM", "WHERE",
            "GROUP BY", "HAVING", "ORDER BY", "ASC", "DESC",
            "LIMIT", "OFFSET",

            // Joins
            "JOIN", "INNER JOIN", "LEFT JOIN", "RIGHT JOIN", "CROSS JOIN",
            "ON", "USING",

            // Set operations
            "UNION", "UNION ALL", "INTERSECT", "EXCEPT",

            // Column/expression keywords
            "AS", "NULL", "DEFAULT", "ROWID",
            "CASE", "WHEN", "THEN", "ELSE", "END",

            // Logical operators
            "AND", "OR", "NOT",
            "IN", "NOT IN",
            "BETWEEN", "NOT BETWEEN",
            "EXISTS", "NOT EXISTS",
            "IS NULL", "IS NOT NULL",
            "LIKE", "NOT LIKE", "GLOB", "REGEXP",

            // Window functions
            "OVER", "PARTITION BY",
            "ROW_NUMBER", "RANK", "DENSE_RANK",
            "LAG", "LEAD",
            "FIRST_VALUE", "LAST_VALUE",
        ];
    }
}

/// <summary>
/// Represents the complete SQL schema for IntelliSense.
/// </summary>
public class SqlSchema
{
    public List<TableSchema> Tables { get; init; } = [];
    public List<FunctionSchema> Functions { get; init; } = [];
    public List<string> Keywords { get; init; } = [];
}

/// <summary>
/// Represents a database table schema.
/// </summary>
public class TableSchema
{
    public string Name { get; init; } = string.Empty;
    public List<ColumnSchema> Columns { get; init; } = [];
}

/// <summary>
/// Represents a table column schema.
/// </summary>
public class ColumnSchema
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public bool IsNullable { get; init; }
    public bool IsPrimaryKey { get; init; }
}

/// <summary>
/// Represents a SQL function for IntelliSense.
/// </summary>
public class FunctionSchema
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
}
