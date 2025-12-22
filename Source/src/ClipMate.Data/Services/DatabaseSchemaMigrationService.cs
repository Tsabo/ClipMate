using System.Data;
using ClipMate.Data.Schema.Abstractions;
using ClipMate.Data.Schema.EntityFramework;
using ClipMate.Data.Schema.Models;
using ClipMate.Data.Schema.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Service for managing database schema migrations.
/// Can be called at any time for any database context.
/// Automatically backs up databases before migration.
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

        // Use the existing connection from the context to support in-memory databases
        var connection = context.Database.GetDbConnection();
        var shouldCloseConnection = false;

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
            shouldCloseConnection = true;
        }

        try
        {
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
                _logger?.LogWarning("Schema validation warnings: {Warnings}", string.Join(", ", validationResult.Warnings));

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
                _logger?.LogWarning("Migration warnings: {Warnings}", string.Join(", ", diff.Warnings));

            // Backup database before migration
            var databasePath = GetDatabasePath(connectionString);
            if (!string.IsNullOrEmpty(databasePath))
                await BackupDatabaseAsync(databasePath, connection);

            // Apply migrations with logging hook
            var hook = new LoggingMigrationHook(_logger);
            var migrator = new SqliteSchemaMigrator(connection, hook);

            var result = await migrator.MigrateAsync(diff, false, cancellationToken);

            if (!result.Success)
            {
                _logger?.LogError("Schema migration failed: {Errors}", string.Join(", ", result.Errors));

                throw new InvalidOperationException($"Schema migration failed: {string.Join(", ", result.Errors)}");
            }

            _logger?.LogInformation("Database schema migration completed successfully");
        }
        finally
        {
            if (shouldCloseConnection && connection.State == ConnectionState.Open)
                await connection.CloseAsync();
        }
    }

    /// <summary>
    /// Backs up a database file before migration.
    /// Creates a timestamped backup in a 'Backups' subfolder.
    /// </summary>
    private Task BackupDatabaseAsync(string databasePath, IDbConnection connection)
    {
        try
        {
            // Close connection temporarily for file copy
            var wasOpen = connection.State == ConnectionState.Open;
            if (wasOpen)
                connection.Close();

            if (!File.Exists(databasePath))
            {
                _logger?.LogWarning("Database file not found for backup: {Path}", databasePath);
                if (wasOpen)
                    connection.Open();

                return Task.CompletedTask;
            }

            // Create Backups directory in same location as database
            var databaseDirectory = Path.GetDirectoryName(databasePath) ?? string.Empty;
            var backupDirectory = Path.Combine(databaseDirectory, "MigrationBackups");

            if (!Directory.Exists(backupDirectory))
            {
                Directory.CreateDirectory(backupDirectory);
                _logger?.LogInformation("Created migration backup directory: {Path}", backupDirectory);
            }

            // Create backup filename with timestamp
            var databaseFileName = Path.GetFileNameWithoutExtension(databasePath);
            var databaseExtension = Path.GetExtension(databasePath);
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var backupFileName = $"{databaseFileName}-migration-{timestamp}{databaseExtension}";
            var backupPath = Path.Combine(backupDirectory, backupFileName);

            // Copy database file
            _logger?.LogInformation("Creating pre-migration database backup: {BackupPath}", backupPath);
            File.Copy(databasePath, backupPath, false);

            var backupFileInfo = new FileInfo(backupPath);
            _logger?.LogInformation("✓ Pre-migration backup created successfully ({Size:N0} bytes)", backupFileInfo.Length);

            // Reopen connection if it was open
            if (wasOpen)
                connection.Open();

            // Clean up old backups (keep last 10)
            CleanupOldBackups(backupDirectory, databaseFileName, databaseExtension);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create database backup. Migration will continue anyway.");

            // Reopen connection if needed
            if (connection.State != ConnectionState.Open)
                connection.Open();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Cleans up old backup files, keeping only the most recent 10.
    /// </summary>
    private void CleanupOldBackups(string backupDirectory, string databaseFileName, string extension)
    {
        try
        {
            var searchPattern = $"{databaseFileName}-migration-*{extension}";
            var backupFiles = Directory.GetFiles(backupDirectory, searchPattern)
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTime)
                .ToList();

            if (backupFiles.Count > 10)
            {
                var filesToDelete = backupFiles.Skip(10).ToList();
                foreach (var file in filesToDelete)
                {
                    file.Delete();
                    _logger?.LogDebug("Deleted old migration backup: {FileName}", file.Name);
                }

                _logger?.LogInformation("Cleaned up {Count} old migration backup file(s)", filesToDelete.Count);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to clean up old backups");
        }
    }

    /// <summary>
    /// Extracts the database file path from a SQLite connection string.
    /// </summary>
    private static string? GetDatabasePath(string connectionString)
    {
        // Handle various SQLite connection string formats
        // Data Source=path; Filename=path; URI=file:path
        var patterns = new[] { "Data Source=", "Filename=", "URI=file:" };

        foreach (var pattern in patterns)
        {
            var index = connectionString.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                var startIndex = index + pattern.Length;
                var endIndex = connectionString.IndexOf(';', startIndex);
                var path = endIndex > startIndex
                    ? connectionString.Substring(startIndex, endIndex - startIndex)
                    : connectionString.Substring(startIndex);

                // Clean up the path
                path = path.Trim().Trim('"', '\'');

                // Expand environment variables
                path = Environment.ExpandEnvironmentVariables(path);

                return path;
            }
        }

        return null;
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

            foreach (var item in context.Diff.Operations)
            {
                _logger?.LogDebug("  {OperationType}: {Description}",
                    item.Type, GetOperationDescription(item));
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
                _logger?.LogError("Migration failed: {Errors}", string.Join(", ", result.Errors));

            return Task.CompletedTask;
        }

        private static string GetOperationDescription(MigrationOperation operation)
        {
            return operation.Type switch
            {
                MigrationOperationType.CreateTable => $"Create table '{operation.TableName}'",
                MigrationOperationType.AddColumn => $"Add column '{operation.ColumnName}' to '{operation.TableName}'",
                MigrationOperationType.CreateIndex => $"Create index '{operation.IndexName}'",
                var _ => operation.Type.ToString(),
            };
        }
    }
}
