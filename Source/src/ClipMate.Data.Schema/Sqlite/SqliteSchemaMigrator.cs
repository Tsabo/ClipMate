using System.Data;
using System.Data.Common;
using ClipMate.Data.Schema.Abstractions;
using ClipMate.Data.Schema.Models;

namespace ClipMate.Data.Schema.Sqlite;

/// <summary>
/// Applies schema migrations to SQLite databases.
/// </summary>
public class SqliteSchemaMigrator : ISchemaMigrator
{
    private readonly DbConnection _connection;
    private readonly IMigrationHook? _hook;

    public SqliteSchemaMigrator(DbConnection connection, IMigrationHook? hook = null)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _hook = hook;
    }

    public async Task<MigrationResult> MigrateAsync(SchemaDiff diff,
        bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        var result = new MigrationResult { Success = false };

        if (!diff.HasChanges)
        {
            result = result with { Success = true };
            result.Warnings.Add("No schema changes to apply");
            return result;
        }

        var context = new MigrationContext
        {
            Diff = diff,
            IsDryRun = dryRun,
            Connection = dryRun
                ? null
                : _connection,
        };

        try
        {
            if (_hook != null)
                await _hook.OnBeforeMigrationAsync(context, cancellationToken);

            if (dryRun)
            {
                foreach (var item in diff.Operations)
                    result.SqlExecuted.Add(item.Sql);

                result = result with { Success = true };
            }
            else
                result = await ApplyMigrationsAsync(diff, result, cancellationToken);

            // Copy warnings from SchemaDiff to result
            foreach (var item in diff.Warnings)
                result.Warnings.Add(item);

            if (_hook != null && result.Success)
                await _hook.OnAfterMigrationAsync(context, result, cancellationToken);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Migration failed: {ex.Message}");
        }

        return result;
    }

    private async Task<MigrationResult> ApplyMigrationsAsync(SchemaDiff diff,
        MigrationResult result,
        CancellationToken cancellationToken)
    {
        if (_connection.State != ConnectionState.Open)
            await _connection.OpenAsync(cancellationToken);

        await using var transaction = await _connection.BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (var item in diff.Operations)
            {
                await using var command = _connection.CreateCommand();
                command.CommandText = item.Sql;
                command.Transaction = transaction;

                await command.ExecuteNonQueryAsync(cancellationToken);
                result.SqlExecuted.Add(item.Sql);
            }

            await transaction.CommitAsync(cancellationToken);
            return result with { Success = true };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            result.Errors.Add($"Transaction rolled back: {ex.Message}");
            throw;
        }
    }
}
