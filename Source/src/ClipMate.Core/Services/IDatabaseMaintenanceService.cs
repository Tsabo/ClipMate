using ClipMate.Core.Models.Configuration;

namespace ClipMate.Core.Services;

/// <summary>
/// Service for performing database maintenance operations including backup, restore, and cleanup.
/// </summary>
public interface IDatabaseMaintenanceService
{
    /// <summary>
    /// Creates a backup of the specified database.
    /// </summary>
    /// <param name="databaseConfig">The database configuration to backup.</param>
    /// <param name="backupDirectory">The directory where the backup ZIP file will be created.</param>
    /// <param name="progress">Optional progress reporter for tracking backup progress.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The full path to the created backup ZIP file.</returns>
    Task<string> BackupDatabaseAsync(DatabaseConfiguration databaseConfig,
        string backupDirectory,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores a database from a backup ZIP file.
    /// </summary>
    /// <param name="backupZipPath">The path to the backup ZIP file.</param>
    /// <param name="databaseConfig">The database configuration to restore to.</param>
    /// <param name="progress">Optional progress reporter for tracking restore progress.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RestoreDatabaseAsync(string backupZipPath,
        DatabaseConfiguration databaseConfig,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Empties the trash by permanently deleting all clips marked as deleted.
    /// </summary>
    /// <param name="databaseConfig">The database configuration.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of clips permanently deleted.</returns>
    Task<int> EmptyTrashAsync(DatabaseConfiguration databaseConfig,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a simple database repair using SQLite VACUUM command.
    /// </summary>
    /// <param name="databaseConfig">The database configuration.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RepairDatabaseAsync(DatabaseConfiguration databaseConfig,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a comprehensive database repair including backup, export, rebuild, and cleanup.
    /// </summary>
    /// <param name="databaseConfig">The database configuration.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ComprehensiveRepairDatabaseAsync(DatabaseConfiguration databaseConfig,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs cleanup to purge old clips based on the database's PurgeDays setting.
    /// </summary>
    /// <param name="databaseConfig">The database configuration.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of clips purged.</returns>
    Task<int> RunCleanupAsync(DatabaseConfiguration databaseConfig,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks which databases are due for backup based on their AllowBackup flag and LastBackupDate.
    /// </summary>
    /// <param name="allDatabases">All database configurations to check.</param>
    /// <param name="backupIntervalDays">Number of days between backups (default: 7).</param>
    /// <returns>List of databases that need backup.</returns>
    Task<List<DatabaseConfiguration>> CheckBackupDueAsync(IEnumerable<DatabaseConfiguration> allDatabases,
        int backupIntervalDays = 7);
}
