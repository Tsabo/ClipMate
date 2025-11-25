using System.Text.Json;
using ClipMate.Data.Schema.Models;

namespace ClipMate.Data.Schema.Serialization;

/// <summary>
/// Serializes and deserializes schema definitions to/from JSON.
/// </summary>
public class SchemaSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public string ToJson(SchemaDefinition schema)
    {
        return JsonSerializer.Serialize(schema, Options);
    }

    public SchemaDefinition FromJson(string json)
    {
        return JsonSerializer.Deserialize<SchemaDefinition>(json, Options)
            ?? throw new InvalidOperationException("Failed to deserialize schema");
    }

    public async Task ExportToFileAsync(SchemaDefinition schema, string filePath, CancellationToken cancellationToken = default)
    {
        var json = ToJson(schema);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }

    public async Task<SchemaDefinition> ImportFromFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        return FromJson(json);
    }
}
