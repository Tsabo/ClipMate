using ClipMate.Data.Schema.Models;

namespace ClipMate.Data.Schema.Abstractions;

/// <summary>
/// Interface for reading schema from various sources.
/// </summary>
public interface ISchemaReader
{
    Task<SchemaDefinition> ReadSchemaAsync(CancellationToken cancellationToken = default);
}
