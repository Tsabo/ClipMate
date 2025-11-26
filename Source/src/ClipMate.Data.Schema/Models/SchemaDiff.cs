using System.Text.Json.Serialization;

namespace ClipMate.Data.Schema.Models;

/// <summary>
/// Represents differences between two schemas.
/// </summary>
public class SchemaDiff
{
    [JsonPropertyName("operations")]
    public List<MigrationOperation> Operations { get; set; } = [];

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];

    [JsonPropertyName("hasChanges")]
    public bool HasChanges => Operations.Any();
}
