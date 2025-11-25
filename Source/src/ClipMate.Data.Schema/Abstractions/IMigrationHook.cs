using ClipMate.Data.Schema.Models;

namespace ClipMate.Data.Schema.Abstractions;

/// <summary>
/// Interface for migration lifecycle hooks.
/// </summary>
public interface IMigrationHook
{
    Task OnBeforeMigrationAsync(MigrationContext context, CancellationToken cancellationToken = default);
    Task OnAfterMigrationAsync(MigrationContext context, MigrationResult result, CancellationToken cancellationToken = default);
}
