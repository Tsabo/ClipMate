using System.Text.Json.Serialization;

namespace ClipMate.Data.Schema.Models;

/// <summary>
/// Types of migration operations.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MigrationOperationType
{
    CreateTable,
    AddColumn,
    CreateIndex,
}
