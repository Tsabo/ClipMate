using System.Text.Json.Serialization;

namespace ClipMate.Data.Schema.Models;

/// <summary>
/// Result of schema validation.
/// </summary>
public class ValidationResult
{
    [JsonPropertyName("isValid")]
    public bool IsValid => !Errors.Any();

    [JsonPropertyName("errors")]
    public List<string> Errors { get; set; } = new();

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();

    [JsonPropertyName("info")]
    public List<string> Info { get; set; } = new();
}
