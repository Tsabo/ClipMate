using System.Text.Json.Serialization;

namespace ClipMate.Data.Schema.Models;

/// <summary>
/// Represents a single migration operation.
/// </summary>
public class MigrationOperation
{
    [JsonPropertyName("type")]
    public required MigrationOperationType Type { get; set; }

    [JsonPropertyName("tableName")]
    public string? TableName { get; set; }

    [JsonPropertyName("columnName")]
    public string? ColumnName { get; set; }

    [JsonPropertyName("indexName")]
    public string? IndexName { get; set; }

    [JsonPropertyName("sql")]
    public required string Sql { get; set; }
}

/// <summary>
/// Types of migration operations.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MigrationOperationType
{
    CreateTable,
    AddColumn,
    CreateIndex
}
