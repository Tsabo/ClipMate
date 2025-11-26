using System.Text.Json;
using System.Text.Json.Serialization;
using ClipMate.Data.Schema.Models;

namespace ClipMate.Data.Schema.Serialization;

/// <summary>
/// Serializes and deserializes schema definitions to/from JSON.
/// </summary>
public class SchemaSerializer
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string ToJson(SchemaDefinition schema) => JsonSerializer.Serialize(schema, _options);

    public SchemaDefinition FromJson(string json)
    {
        return JsonSerializer.Deserialize<SchemaDefinition>(json, _options)
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
