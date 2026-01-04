using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Platform;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Background service that schedules periodic database maintenance tasks.
/// Runs retention enforcement and cleanup only when the system is idle.
/// </summary>
public class MaintenanceSchedulerService : IHostedService, IDisposable
{
    private readonly IConfigurationService _configurationService;
    private readonly IDatabaseMaintenanceService _databaseMaintenanceService;
    private readonly IWin32IdleDetector _idleDetector;
    private readonly TimeSpan _idleThreshold;
    private readonly ILogger<MaintenanceSchedulerService> _logger;
    private readonly TimeSpan _maintenanceInterval;
    private readonly IRetentionEnforcementService _retentionService;
    private Timer? _timer;

    public MaintenanceSchedulerService(IRetentionEnforcementService retentionService,
        IDatabaseMaintenanceService databaseMaintenanceService,
        IConfigurationService configurationService,
        IWin32IdleDetector idleDetector,
        ILogger<MaintenanceSchedulerService> logger)
    {
        _retentionService = retentionService ?? throw new ArgumentNullException(nameof(retentionService));
        _databaseMaintenanceService = databaseMaintenanceService ?? throw new ArgumentNullException(nameof(databaseMaintenanceService));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _idleDetector = idleDetector ?? throw new ArgumentNullException(nameof(idleDetector));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Default: Run maintenance every hour
        _maintenanceInterval = TimeSpan.FromHours(1);

        // Default: Wait for 5 minutes of idle time before running maintenance
        _idleThreshold = TimeSpan.FromMinutes(5);
    }

    public void Dispose()
    {
        _timer?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Maintenance scheduler started. Interval: {Interval}, Idle threshold: {IdleThreshold}",
            _maintenanceInterval,
            _idleThreshold);

        _timer = new Timer(
            RunMaintenance,
            null,
            _maintenanceInterval,
            _maintenanceInterval);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Maintenance scheduler stopping");
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    private async void RunMaintenance(object? state)
    {
        try
        {
            // Only run maintenance when system has been idle for at least 5 minutes
            // This prevents interfering with active user work
            if (!_idleDetector.IsIdle(_idleThreshold))
            {
                _logger.LogDebug(
                    "Skipping maintenance - system not idle (threshold: {IdleThreshold})",
                    _idleThreshold);

                return;
            }

            _logger.LogInformation("Starting scheduled maintenance");

            var config = _configurationService.Configuration;

            // Process each database independently with its own configuration
            foreach (var (databaseKey, dbConfig) in config.Databases)
            {
                _logger.LogDebug("Processing maintenance for database: {Name} (Key: {Key})", dbConfig.Name, databaseKey);

                // ===== TASK 1: Retention Enforcement (Always Run) =====
                // Move clips through the cascade: Collection → Overflow → Trashcan
                // This respects MaxClips, MaxBytes, and MaxAgeDays per collection
                var clipsProcessed = await _retentionService.EnforceAllCollectionsAsync(databaseKey);
                _logger.LogDebug("Retention enforcement completed. Clips moved/deleted: {ClipsProcessed}", clipsProcessed);

                // ===== TASK 2: Cleanup (Conditional on CleanupMethod) =====
                // Permanently delete clips from Trashcan if CleanupMethod is AfterHourIdle
                // Respects PurgeDays setting (default 7 days after deletion)
                if (dbConfig.CleanupMethod != CleanupMethod.AfterHourIdle)
                    continue;

                _logger.LogDebug("Running cleanup for database '{Name}' (CleanupMethod: {Method})",
                    dbConfig.Name, dbConfig.CleanupMethod);

                var progress = new Progress<string>(message => _logger.LogDebug("Cleanup: {Message}", message));
                await _databaseMaintenanceService.RunCleanupAsync(dbConfig, progress);
            }

            // ===== TASK 3: Old Backup Cleanup (Once Per Hour) =====
            // Delete backup files older than 14 days across all backup directories
            // This runs once per hour regardless of database count
            var backupDirs = config.Databases.Values
                .Select(p => Environment.ExpandEnvironmentVariables(p.BackupDirectory))
                .Distinct()
                .Where(dir => !string.IsNullOrWhiteSpace(dir));

            foreach (var item in backupDirs)
            {
                _logger.LogDebug("Cleaning up old backups in: {Dir}", item);
                var deletedCount = await _databaseMaintenanceService.CleanupOldBackupsAsync(item, 14);
                if (deletedCount > 0)
                    _logger.LogInformation("Deleted {Count} old backup files from {Dir}", deletedCount, item);
            }

            _logger.LogInformation("Scheduled maintenance completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during scheduled maintenance");
        }
    }
}
