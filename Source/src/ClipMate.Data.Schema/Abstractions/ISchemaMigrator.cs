using ClipMate.Data.Schema.Models;

namespace ClipMate.Data.Schema.Abstractions;

/// <summary>
/// Interface for applying schema migrations.
/// </summary>
public interface ISchemaMigrator
{
    Task<MigrationResult> MigrateAsync(SchemaDiff diff,
        bool dryRun = false,
        CancellationToken cancellationToken = default);
}
