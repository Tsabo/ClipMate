using System.Data.Common;

namespace ClipMate.Data.Schema.Models;

/// <summary>
/// Context passed to migration hooks.
/// </summary>
public class MigrationContext
{
    public required SchemaDiff Diff { get; init; }
    public required bool IsDryRun { get; init; }
    public DbConnection? Connection { get; init; }
    public Dictionary<string, object> Properties { get; init; } = new();
}
