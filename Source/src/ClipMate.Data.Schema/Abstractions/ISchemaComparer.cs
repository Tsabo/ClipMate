using ClipMate.Data.Schema.Models;

namespace ClipMate.Data.Schema.Abstractions;

/// <summary>
/// Interface for comparing two schemas and generating differences.
/// </summary>
public interface ISchemaComparer
{
    SchemaDiff Compare(SchemaDefinition current, SchemaDefinition expected);
}
