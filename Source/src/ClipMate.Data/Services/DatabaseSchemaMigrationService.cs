using ClipMate.Data.Schema.Abstractions;
using ClipMate.Data.Schema.EntityFramework;
using ClipMate.Data.Schema.Models;
using ClipMate.Data.Schema.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Service for managing database schema migrations.
/// Can be called at any time for any database context.
/// </summary>
public class DatabaseSchemaMigrationService
{
    private readonly ILogger<DatabaseSchemaMigrationService>? _logger;

    public DatabaseSchemaMigrationService(ILogger<DatabaseSchemaMigrationService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Migrates the database schema to match the EF Core model.
    /// </summary>
    /// <param name="context">The database context to migrate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task MigrateAsync(ClipMateDbContext context, CancellationToken cancellationToken = default)
    {
        var connectionString = context.Database.GetConnectionString();
        if (string.IsNullOrEmpty(connectionString))
        {
            _logger?.LogWarning("No connection string found, skipping schema migration");
            return;
        }

        _logger?.LogInformation("Starting database schema migration");

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var options = new SchemaOptions();

        // Read current schema from database
        var schemaReader = new SqliteSchemaReader(connection, options);
        var currentSchema = await schemaReader.ReadSchemaAsync(cancellationToken);

        // Read expected schema from EF Core model
        var efSchemaReader = new EFCoreSchemaReader(context.Model, options);
        var expectedSchema = await efSchemaReader.ReadSchemaAsync(cancellationToken);

        // Validate expected schema
        var validator = new SqliteSchemaValidator();
        var validationResult = validator.Validate(expectedSchema);

        if (!validationResult.IsValid)
        {
            _logger?.LogError("Schema validation failed: {Errors}", string.Join(", ", validationResult.Errors));
            throw new InvalidOperationException($"Schema validation failed: {string.Join(", ", validationResult.Errors)}");
        }

        if (validationResult.Warnings.Count > 0)
        {
            _logger?.LogWarning("Schema validation warnings: {Warnings}", string.Join(", ", validationResult.Warnings));
        }

        // Compare schemas
        var comparer = new SqliteSchemaComparer();
        var diff = comparer.Compare(currentSchema, expectedSchema);

        if (!diff.HasChanges)
        {
            _logger?.LogInformation("Database schema is up to date");
            return;
        }

        _logger?.LogInformation("Schema changes detected: {OperationCount} operations", diff.Operations.Count);

        if (diff.Warnings.Count > 0)
        {
            _logger?.LogWarning("Migration warnings: {Warnings}", string.Join(", ", diff.Warnings));
        }

        // Apply migrations with logging hook
        var hook = new LoggingMigrationHook(_logger);
        var migrator = new SqliteSchemaMigrator(connection, hook);

        var result = await migrator.MigrateAsync(diff, dryRun: false, cancellationToken);

        if (!result.Success)
        {
            _logger?.LogError("Schema migration failed: {Errors}", string.Join(", ", result.Errors));
            throw new InvalidOperationException($"Schema migration failed: {string.Join(", ", result.Errors)}");
        }

        _logger?.LogInformation("Database schema migration completed successfully");
    }

    /// <summary>
    /// Logging hook for migration operations.
    /// </summary>
    private class LoggingMigrationHook : IMigrationHook
    {
        private readonly ILogger? _logger;

        public LoggingMigrationHook(ILogger? logger)
        {
            _logger = logger;
        }

        public Task OnBeforeMigrationAsync(MigrationContext context, CancellationToken cancellationToken)
        {
            _logger?.LogInformation("Starting migration with {OperationCount} operations (Dry run: {IsDryRun})",
                context.Diff.Operations.Count, context.IsDryRun);

            foreach (var operation in context.Diff.Operations)
            {
                _logger?.LogDebug("  {OperationType}: {Description}",
                    operation.Type, GetOperationDescription(operation));
            }

            return Task.CompletedTask;
        }

        public Task OnAfterMigrationAsync(MigrationContext context, MigrationResult result, CancellationToken cancellationToken)
        {
            if (result.Success)
            {
                _logger?.LogInformation("Migration completed successfully. Executed {SqlCount} SQL statements",
                    result.SqlExecuted.Count);
            }
            else
            {
                _logger?.LogError("Migration failed: {Errors}", string.Join(", ", result.Errors));
            }

            return Task.CompletedTask;
        }

        private static string GetOperationDescription(MigrationOperation operation)
        {
            return operation.Type switch
            {
                MigrationOperationType.CreateTable => $"Create table '{operation.TableName}'",
                MigrationOperationType.AddColumn => $"Add column '{operation.ColumnName}' to '{operation.TableName}'",
                MigrationOperationType.CreateIndex => $"Create index '{operation.IndexName}'",
                _ => operation.Type.ToString()
            };
        }
    }
}
