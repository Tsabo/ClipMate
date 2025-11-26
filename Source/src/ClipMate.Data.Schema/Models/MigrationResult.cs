using System.Text.Json.Serialization;

namespace ClipMate.Data.Schema.Models;

/// <summary>
/// Result of a migration operation.
/// </summary>
public record MigrationResult
{
    [JsonPropertyName("success")]
    public required bool Success { get; init; }

    [JsonPropertyName("sqlExecuted")]
    public List<string> SqlExecuted { get; init; } = [];

    [JsonPropertyName("errors")]
    public List<string> Errors { get; init; } = [];

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; init; } = [];
}
