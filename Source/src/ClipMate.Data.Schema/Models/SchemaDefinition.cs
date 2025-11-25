using System.Text.Json.Serialization;

namespace ClipMate.Data.Schema.Models;

/// <summary>
/// Represents a complete database schema.
/// </summary>
public class SchemaDefinition
{
    [JsonPropertyName("tables")]
    public Dictionary<string, TableDefinition> Tables { get; set; } = new();
}
