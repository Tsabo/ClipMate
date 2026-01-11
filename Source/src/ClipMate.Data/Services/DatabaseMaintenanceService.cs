using System.IO.Compression;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Service for performing database maintenance operations.
/// </summary>
public class DatabaseMaintenanceService : IDatabaseMaintenanceService
{
    private readonly ILogger<DatabaseMaintenanceService> _logger;

    public DatabaseMaintenanceService(ILogger<DatabaseMaintenanceService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> BackupDatabaseAsync(DatabaseConfiguration databaseConfig,
        string backupDirectory,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report($"Starting backup of database '{databaseConfig.Name}'...");

        var expandedDbFile = Environment.ExpandEnvironmentVariables(databaseConfig.FilePath);
        var expandedBackupPath = Environment.ExpandEnvironmentVariables(backupDirectory);

        // Ensure backup directory exists
        Directory.CreateDirectory(expandedBackupPath);

        // Generate backup filename
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        var backupFileName = $"ClipMate_DB_{SanitizeFileName(databaseConfig.Name)}_{timestamp}.zip";
        var backupZipPath = Path.Join(expandedBackupPath, backupFileName);

        progress?.Report($"Creating backup file: {backupFileName}");

        // Database files to backup
        var dbFile = expandedDbFile;
        var dbShmFile = $"{expandedDbFile}-shm";
        var dbWalFile = $"{expandedDbFile}-wal";

        if (!File.Exists(dbFile))
            throw new FileNotFoundException($"Database file not found: {dbFile}");

        // Checkpoint WAL and close all connections for THIS specific database
        var optionsBuilder = new DbContextOptionsBuilder<ClipMateDbContext>();
        optionsBuilder.UseSqlite($"Data Source={dbFile}");

        await using (var context = new ClipMateDbContext(optionsBuilder.Options))
        {
            try
            {
                // Force SQLite to checkpoint the WAL file
                await context.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE);", cancellationToken);
                await context.Database.CloseConnectionAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to checkpoint WAL for database: {DbFile}", dbFile);
            }
        }

        // Force garbage collection to release file handles
        SqliteConnection.ClearAllPools();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        await Task.Delay(200, cancellationToken);

        // Create backup ZIP
        progress?.Report("Compressing database files...");
        await using (var archive = await ZipFile.OpenAsync(backupZipPath, ZipArchiveMode.Create, cancellationToken))
        {
            // Add main database file (using FileShare.ReadWrite to allow SQLite concurrent access)
            var entry = archive.CreateEntry(Path.GetFileName(dbFile), CompressionLevel.Optimal);
            await using (var entryStream = await entry.OpenAsync(cancellationToken))
            await using (var fileStream = new FileStream(dbFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                await fileStream.CopyToAsync(entryStream, cancellationToken);
            }

            progress?.Report($"Added: {Path.GetFileName(dbFile)}");

            // Add WAL and SHM files if they exist (using FileShare.ReadWrite)
            if (File.Exists(dbShmFile))
            {
                var shmEntry = archive.CreateEntry(Path.GetFileName(dbShmFile), CompressionLevel.Optimal);
                await using (var entryStream = await shmEntry.OpenAsync(cancellationToken))
                await using (var fileStream = new FileStream(dbShmFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    await fileStream.CopyToAsync(entryStream, cancellationToken);
                }

                progress?.Report($"Added: {Path.GetFileName(dbShmFile)}");
            }

            if (File.Exists(dbWalFile))
            {
                var walEntry = archive.CreateEntry(Path.GetFileName(dbWalFile), CompressionLevel.Optimal);
                await using (var entryStream = await walEntry.OpenAsync(cancellationToken))
                await using (var fileStream = new FileStream(dbWalFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    await fileStream.CopyToAsync(entryStream, cancellationToken);
                }

                progress?.Report($"Added: {Path.GetFileName(dbWalFile)}");
            }
        }

        progress?.Report($"Backup completed: {backupZipPath}");
        _logger.LogInformation("Database backup created: {BackupPath}", backupZipPath);

        return backupZipPath;
    }

    /// <inheritdoc />
    public async Task RestoreDatabaseAsync(string backupZipPath,
        DatabaseConfiguration databaseConfig,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report($"Starting restore of database '{databaseConfig.Name}'...");

        if (!File.Exists(backupZipPath))
            throw new FileNotFoundException($"Backup file not found: {backupZipPath}");

        var dbFile = Environment.ExpandEnvironmentVariables(databaseConfig.FilePath);
        var dbDirectory = Path.GetDirectoryName(dbFile) ?? throw new InvalidOperationException("Cannot determine database directory");

        // Close all connections to the database
        progress?.Report("Closing database connections...");
        var optionsBuilder = new DbContextOptionsBuilder<ClipMateDbContext>();
        optionsBuilder.UseSqlite($"Data Source={dbFile}");

        await using (var context = new ClipMateDbContext(optionsBuilder.Options))
        {
            try
            {
                await context.Database.CloseConnectionAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to close database connections for: {DbFile}", dbFile);
            }
        }

        // Give time for connections to close
        await Task.Delay(500, cancellationToken);

        // Force SQLite to release all connection pools
        SqliteConnection.ClearAllPools();
        GC.Collect();
        GC.WaitForPendingFinalizers();

        // Create temporary directory for extraction
        var tempDir = Path.Join(Path.GetTempPath(), $"ClipMate_Restore_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            progress?.Report("Extracting backup files...");
            ZipFile.ExtractToDirectory(backupZipPath, tempDir);

            // Database files
            var dbShmFile = $"{dbFile}-shm";
            var dbWalFile = $"{dbFile}-wal";

            // Backup current database before overwriting
            progress?.Report("Creating safety backup of current database...");
            var safetyBackupDir = Path.Join(dbDirectory, "Restore_Backup");
            Directory.CreateDirectory(safetyBackupDir);
            var safetyTimestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");

            if (File.Exists(dbFile))
                File.Copy(dbFile, Path.Join(safetyBackupDir, $"{databaseConfig.Name}_{safetyTimestamp}.db"), true);

            // Delete existing database files
            progress?.Report("Removing current database files...");
            if (File.Exists(dbFile))
                File.Delete(dbFile);

            if (File.Exists(dbShmFile))
                File.Delete(dbShmFile);

            if (File.Exists(dbWalFile))
                File.Delete(dbWalFile);

            // Copy restored files to database directory
            progress?.Report("Restoring database files...");
            foreach (var item in Directory.GetFiles(tempDir))
            {
                var fileName = Path.GetFileName(item);
                var destFile = Path.Join(dbDirectory, fileName);
                File.Copy(item, destFile, true);
                progress?.Report($"Restored: {fileName}");
            }

            progress?.Report("Database restore completed successfully.");
            _logger.LogInformation("Database restored from backup: {BackupPath}", backupZipPath);
        }
        finally
        {
            // Clean up temporary directory
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    /// <inheritdoc />
    public async Task<int> EmptyTrashAsync(DatabaseConfiguration databaseConfig,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report($"Emptying trash for database '{databaseConfig.Name}'...");

        var dbFile = Environment.ExpandEnvironmentVariables(databaseConfig.FilePath);

        var optionsBuilder = new DbContextOptionsBuilder<ClipMateDbContext>();
        optionsBuilder.UseSqlite($"Data Source={dbFile}");

        await using var context = new ClipMateDbContext(optionsBuilder.Options);

        // Count deleted clips
        var deletedCount = await context.Clips
            .Where(p => p.Del)
            .CountAsync(cancellationToken);

        if (deletedCount == 0)
        {
            progress?.Report("No clips in trash to delete.");
            return 0;
        }

        progress?.Report($"Found {deletedCount} deleted clips. Permanently removing...");

        // Delete clips and their associated data
        await context.Database.ExecuteSqlAsync(
            $"DELETE FROM Clips WHERE Del = 1",
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        // Explicitly close the connection to release file locks
        await context.Database.CloseConnectionAsync();

        progress?.Report($"Successfully deleted {deletedCount} clips from trash.");
        _logger.LogInformation("Emptied trash: {Count} clips deleted from database '{Database}'",
            deletedCount, databaseConfig.Name);

        return deletedCount;
    }

    /// <inheritdoc />
    public async Task RepairDatabaseAsync(DatabaseConfiguration databaseConfig,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report($"Repairing database '{databaseConfig.Name}'...");

        var dbFile = Environment.ExpandEnvironmentVariables(databaseConfig.FilePath);

        if (!File.Exists(dbFile))
            throw new FileNotFoundException($"Database file not found: {dbFile}");

        progress?.Report("Closing database connections...");
        var optionsBuilder = new DbContextOptionsBuilder<ClipMateDbContext>();
        optionsBuilder.UseSqlite($"Data Source={dbFile}");

        await using (var context = new ClipMateDbContext(optionsBuilder.Options))
        {
            try
            {
                await context.Database.CloseConnectionAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to close database connections for: {DbFile}", dbFile);
            }
        }

        await Task.Delay(100, cancellationToken);

        progress?.Report("Running VACUUM to compact and repair database...");

        // Open direct connection to run VACUUM
        var connectionString = $"Data Source={dbFile}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "VACUUM;";
        await command.ExecuteNonQueryAsync(cancellationToken);

        progress?.Report("Database repair completed successfully.");
        _logger.LogInformation("Database repaired: {Database}", databaseConfig.Name);
    }

    /// <inheritdoc />
    public async Task ComprehensiveRepairDatabaseAsync(DatabaseConfiguration databaseConfig,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report($"Starting comprehensive repair of database '{databaseConfig.Name}'...");

        // Step 1: Create automatic backup
        progress?.Report("Step 1/5: Creating automatic backup...");
        var dbFile = Environment.ExpandEnvironmentVariables(databaseConfig.FilePath);
        var dbDirectory = Path.GetDirectoryName(dbFile) ?? throw new InvalidOperationException("Cannot determine database directory");
        var repairBackupDir = Path.Join(dbDirectory, "Repair_Backup");
        Directory.CreateDirectory(repairBackupDir);

        await BackupDatabaseAsync(databaseConfig, repairBackupDir, progress, cancellationToken);

        // Step 2: Empty trash
        progress?.Report("Step 2/5: Emptying trash...");
        await EmptyTrashAsync(databaseConfig, progress, cancellationToken);

        // Step 3: Run cleanup
        progress?.Report("Step 3/5: Running cleanup to purge old clips...");
        await RunCleanupAsync(databaseConfig, progress, cancellationToken);

        // Step 4: Export and rebuild database
        progress?.Report("Step 4/5: Exporting and rebuilding database...");
        await ExportAndRebuildDatabaseAsync(databaseConfig, progress, cancellationToken);

        // Step 5: Vacuum
        progress?.Report("Step 5/5: Compacting database...");
        await RepairDatabaseAsync(databaseConfig, progress, cancellationToken);

        progress?.Report("Comprehensive repair completed successfully.");
        _logger.LogInformation("Comprehensive repair completed for database: {Database}", databaseConfig.Name);
    }

    /// <inheritdoc />
    public async Task<int> RunCleanupAsync(DatabaseConfiguration databaseConfig,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report($"Running cleanup for database '{databaseConfig.Name}'...");

        if (databaseConfig.PurgeDays <= 0)
        {
            progress?.Report("Cleanup is disabled (PurgeDays = 0).");
            return 0;
        }

        var dbFile = Environment.ExpandEnvironmentVariables(databaseConfig.FilePath);

        var optionsBuilder = new DbContextOptionsBuilder<ClipMateDbContext>();
        optionsBuilder.UseSqlite($"Data Source={dbFile}");

        await using var context = new ClipMateDbContext(optionsBuilder.Options);

        var cutoffDate = DateTimeOffset.UtcNow.AddDays(-databaseConfig.PurgeDays);
        progress?.Report($"Purging clips older than {databaseConfig.PurgeDays} days (before {cutoffDate:yyyy-MM-dd})...");

        // Use raw SQL with parameterized query - DateTimeOffset stored as ticks in SQLite
        var cutoffTicks = cutoffDate.UtcTicks;

        // Count clips to be purged (only clips marked as deleted)
        var countResult = await context.Database
            .SqlQuery<int>($"SELECT COUNT(*) as Value FROM Clips WHERE Del = 1 AND DelDate IS NOT NULL AND DelDate < {cutoffTicks}")
            .ToListAsync(cancellationToken);

        var purgeCount = countResult.FirstOrDefault();

        if (purgeCount == 0)
        {
            progress?.Report("No clips to purge.");
            return 0;
        }

        progress?.Report($"Found {purgeCount} clips to purge. Deleting...");

        // Delete old clips using parameterized query (only clips marked as deleted)
        await context.Database.ExecuteSqlAsync(
            $"DELETE FROM Clips WHERE Del = 1 AND DelDate IS NOT NULL AND DelDate < {cutoffTicks}",
            cancellationToken);

        // Explicitly close the connection to release file locks
        await context.Database.CloseConnectionAsync();

        progress?.Report($"Successfully purged {purgeCount} old clips.");
        _logger.LogInformation("Cleanup completed: {Count} clips purged from database '{Database}'",
            purgeCount, databaseConfig.Name);

        return purgeCount;
    }

    /// <inheritdoc />
    public async Task<List<DatabaseConfiguration>> CheckBackupDueAsync(IEnumerable<DatabaseConfiguration> allDatabases,
        int backupIntervalDays = 7)
    {
        var databasesDue = new List<DatabaseConfiguration>();

        foreach (var item in allDatabases)
        {
            if (!item.AllowBackup)
                continue;

            // Check if never backed up
            if (item.LastBackupDate == null)
            {
                databasesDue.Add(item);
                continue;
            }

            // Check if backup is overdue based on interval
            var nextBackupDue = item.LastBackupDate.Value.AddDays(backupIntervalDays);
            if (DateTime.Now >= nextBackupDue)
                databasesDue.Add(item);
        }

        return await Task.FromResult(databasesDue);
    }

    /// <inheritdoc />
    public Task<int> CleanupOldBackupsAsync(string backupDirectory,
        int retentionDays = 14,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report($"Cleaning up backup files older than {retentionDays} days...");

        var expandedBackupPath = Environment.ExpandEnvironmentVariables(backupDirectory);

        // Validate backup directory exists
        if (!Directory.Exists(expandedBackupPath))
        {
            progress?.Report("Backup directory does not exist. Skipping cleanup.");
            return Task.FromResult(0);
        }

        // Calculate cutoff date based on retention policy
        var cutoffDate = DateTime.Now.AddDays(-retentionDays);
        var backupFiles = Directory.GetFiles(expandedBackupPath, "ClipMate_DB_*.zip");
        var deletedCount = 0;

        // Delete backup files older than retention period
        foreach (var item in backupFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileInfo = new FileInfo(item);
            if (fileInfo.LastWriteTime >= cutoffDate)
                continue;

            try
            {
                File.Delete(item);
                deletedCount++;
                progress?.Report($"Deleted old backup: {fileInfo.Name}");
                _logger.LogInformation("Deleted old backup file: {BackupFile} (Age: {Age} days)",
                    item, (DateTime.Now - fileInfo.LastWriteTime).TotalDays);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete old backup file: {BackupFile}", item);
                progress?.Report($"Warning: Could not delete {fileInfo.Name}");
            }
        }

        progress?.Report($"Cleanup complete. Deleted {deletedCount} old backup file(s).");
        return Task.FromResult(deletedCount);
    }

    /// <inheritdoc />
    public async Task<bool> CheckDatabaseIntegrityAsync(DatabaseConfiguration databaseConfig,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report($"Checking integrity of database '{databaseConfig.Name}'...");

        var expandedDbFile = Environment.ExpandEnvironmentVariables(databaseConfig.FilePath);

        // Validate database file exists
        if (!File.Exists(expandedDbFile))
        {
            progress?.Report($"Database file not found: {expandedDbFile}");
            _logger.LogWarning("Database file not found during integrity check: {DbFile}", expandedDbFile);
            return false;
        }

        try
        {
            // Open direct connection to run PRAGMA integrity_check
            // This is a lightweight check that verifies database structure without full scan
            var connectionString = $"Data Source={expandedDbFile}";
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";

            var result = await command.ExecuteScalarAsync(cancellationToken);
            var resultString = result?.ToString() ?? string.Empty;

            // SQLite returns "ok" if database is healthy, or error messages if corrupted
            if (resultString.Equals("ok", StringComparison.OrdinalIgnoreCase))
            {
                progress?.Report("Database integrity check passed.");
                _logger.LogInformation("Database integrity check passed for: {DbFile}", expandedDbFile);
                return true;
            }

            progress?.Report($"Database integrity check FAILED: {resultString}");
            _logger.LogError("Database integrity check failed for: {DbFile}. Result: {Result}",
                expandedDbFile, resultString);

            return false;
        }
        catch (Exception ex)
        {
            progress?.Report($"Error during integrity check: {ex.Message}");
            _logger.LogError(ex, "Exception during database integrity check for: {DbFile}", expandedDbFile);
            return false;
        }
    }

    private async Task ExportAndRebuildDatabaseAsync(DatabaseConfiguration databaseConfig,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report("Creating new database structure...");

        var oldDbFile = Environment.ExpandEnvironmentVariables(databaseConfig.FilePath);
        var dbDirectory = Path.GetDirectoryName(oldDbFile) ?? throw new InvalidOperationException("Cannot determine database directory");
        var newDbFile = Path.Join(dbDirectory, "clipmate_new.db");

        // Close connections
        var optionsBuilder = new DbContextOptionsBuilder<ClipMateDbContext>();
        optionsBuilder.UseSqlite($"Data Source={oldDbFile}");

        await using (var context = new ClipMateDbContext(optionsBuilder.Options))
        {
            try
            {
                await context.Database.CloseConnectionAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to close database connections for: {DbFile}", oldDbFile);
            }
        }

        await Task.Delay(100, cancellationToken);

        // Create new database with schema
        var newConnectionString = $"Data Source={newDbFile}";
        await using var newConnection = new SqliteConnection(newConnectionString);
        await newConnection.OpenAsync(cancellationToken);

        // Copy schema and data using SQLite's backup API or SQL commands
        progress?.Report("Copying data to new database...");

        var oldConnectionString = $"Data Source={oldDbFile}";
        await using var oldConnection = new SqliteConnection(oldConnectionString);
        await oldConnection.OpenAsync(cancellationToken);

        // Use SQLite backup API
        oldConnection.BackupDatabase(newConnection);

        await oldConnection.CloseAsync();
        await newConnection.CloseAsync();

        // Replace old database with new one
        progress?.Report("Replacing old database with rebuilt version...");
        File.Delete(oldDbFile);
        File.Move(newDbFile, oldDbFile);

        progress?.Report("Database rebuilt successfully.");
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
    }
}
