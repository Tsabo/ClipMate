using System.Text.Json.Serialization;

namespace ClipMate.Data.Schema.Models;

/// <summary>
/// Represents a column definition.
/// </summary>
public class ColumnDefinition
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("isNullable")]
    public bool IsNullable { get; set; }

    [JsonPropertyName("isPrimaryKey")]
    public bool IsPrimaryKey { get; set; }

    [JsonPropertyName("defaultValue")]
    public string? DefaultValue { get; set; }

    [JsonPropertyName("position")]
    public int Position { get; set; }
}
