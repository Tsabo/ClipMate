using ClipMate.Data.Schema.Models;

namespace ClipMate.Data.Schema.Abstractions;

/// <summary>
/// Interface for validating schema definitions.
/// </summary>
public interface ISchemaValidator
{
    ValidationResult Validate(SchemaDefinition schema);
}
