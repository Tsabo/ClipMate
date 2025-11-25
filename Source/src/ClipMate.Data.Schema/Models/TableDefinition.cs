using System.Text.Json.Serialization;

namespace ClipMate.Data.Schema.Models;

/// <summary>
/// Represents a table schema definition.
/// </summary>
public class TableDefinition
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("columns")]
    public List<ColumnDefinition> Columns { get; set; } = new();

    [JsonPropertyName("indexes")]
    public List<IndexDefinition> Indexes { get; set; } = new();

    [JsonPropertyName("foreignKeys")]
    public List<ForeignKeyDefinition> ForeignKeys { get; set; } = new();

    [JsonPropertyName("createSql")]
    public string? CreateSql { get; set; }
}
