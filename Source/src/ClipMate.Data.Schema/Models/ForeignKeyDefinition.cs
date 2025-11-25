using System.Text.Json.Serialization;

namespace ClipMate.Data.Schema.Models;

/// <summary>
/// Represents a foreign key constraint.
/// </summary>
public class ForeignKeyDefinition
{
    [JsonPropertyName("columnName")]
    public required string ColumnName { get; set; }

    [JsonPropertyName("referencedTable")]
    public required string ReferencedTable { get; set; }

    [JsonPropertyName("referencedColumn")]
    public required string ReferencedColumn { get; set; }

    [JsonPropertyName("onDelete")]
    public string? OnDelete { get; set; }

    [JsonPropertyName("onUpdate")]
    public string? OnUpdate { get; set; }
}
