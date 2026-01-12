using System.Diagnostics;
using ClipMate.App.Services.Initialization;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;

namespace ClipMate.App.Services;

/// <summary>
/// Orchestrates application startup tasks including database validation, initialization pipeline,
/// maintenance tasks, and window creation.
/// </summary>
public class StartupOrchestrationService
{
    private readonly IConfigurationService _configService;
    private readonly ILogger<StartupOrchestrationService> _logger;
    private readonly IDatabaseMaintenanceService _maintenanceService;
    private readonly IServiceProvider _serviceProvider;

    public StartupOrchestrationService(IServiceProvider serviceProvider,
        IConfigurationService configService,
        IDatabaseMaintenanceService maintenanceService,
        ILogger<StartupOrchestrationService> logger)
    {
        _serviceProvider = serviceProvider;
        _configService = configService;
        _maintenanceService = maintenanceService;
        _logger = logger;
    }

    /// <summary>
    /// Runs the complete startup orchestration including initialization pipeline,
    /// coordinators, maintenance tasks, and window creation.
    /// </summary>
    public async Task RunAsync()
    {
        // Run initialization pipeline (database schema, configuration, default data)
        var pipeline = _serviceProvider.GetRequiredService<StartupInitializationPipeline>();
        await pipeline.RunAsync();

        // Initialize coordinators (registers for events)
        _ = _serviceProvider.GetRequiredService<DatabaseMaintenanceCoordinator>();

        // Check if any databases need backup
        var backupService = _serviceProvider.GetRequiredService<BackupOrchestrationService>();
        await backupService.CheckAndPromptForBackupsAsync();

        // Run startup cleanup tasks (if configured)
        await RunStartupMaintenanceTasksAsync();

        // Apply icon configuration and create windows
        await CreateApplicationWindowsAsync();
    }

    /// <summary>
    /// Runs startup maintenance tasks: integrity checks, backup cleanup, and CleanupMethod.AtStartup triggers.
    /// This ensures database health and enforces maintenance policies configured per database.
    /// </summary>
    private async Task RunStartupMaintenanceTasksAsync()
    {
        try
        {
            var config = _configService.Configuration;

            _logger.LogDebug("Running startup maintenance tasks...");

            // ===== TASK 1: Database Integrity Checks =====
            // Perform quick PRAGMA integrity_check on all databases to detect corruption early.
            // If corruption is found, prompt user for automatic repair using ComprehensiveRepairDatabaseAsync.
            _logger.LogDebug("Performing integrity checks on all databases...");
            foreach (var item in config.Databases.Values)
            {
                var isHealthy = await _maintenanceService.CheckDatabaseIntegrityAsync(item);
                if (isHealthy)
                    continue;

                _logger.LogWarning("Database '{Name}' failed integrity check at startup", item.Name);

                // Prompt user for repair
                var result = MessageBox.Show(
                    $"Database '{item.Name}' has integrity issues.\n\n" +
                    "Would you like to attempt automatic repair?\n\n" +
                    "This will close the application, repair the database, and restart.",
                    "Database Corruption Detected",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    continue;

                _logger.LogInformation("User requested repair for database: {Name}", item.Name);

                // Show progress dialog and run comprehensive repair (backup + empty trash + cleanup + rebuild + vacuum)
                var progress = new Progress<string>(p => _logger.LogInformation("Repair: {Message}", p));
                await _maintenanceService.ComprehensiveRepairDatabaseAsync(item, progress);

                MessageBox.Show(
                    $"Database '{item.Name}' has been repaired.\n\nThe application will now restart.",
                    "Repair Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Restart application to load repaired database
                Process.Start(Environment.ProcessPath!);
                Application.Current.Shutdown(0);
                return;
            }

            // ===== TASK 2: Old Backup File Cleanup =====
            // Clean up backup files older than 14 days to prevent disk space issues.
            // Processes all unique backup directories configured across databases.
            var backupDirs = config.Databases.Values
                .Select(p => Environment.ExpandEnvironmentVariables(p.BackupDirectory))
                .Distinct()
                .Where(p => !string.IsNullOrWhiteSpace(p));

            foreach (var item in backupDirs)
            {
                _logger.LogDebug("Cleaning up old backups in: {Dir}", item);
                var deletedCount = await _maintenanceService.CleanupOldBackupsAsync(item);
                if (deletedCount > 0)
                    _logger.LogInformation("Deleted {Count} old backup files from {Dir}", deletedCount, item);
            }

            // ===== TASK 3: CleanupMethod.AtStartup Processing =====
            // Run cleanup (permanently delete clips based on PurgeDays) for databases configured with AtStartup.
            // This respects per-database CleanupMethod configuration.
            var startupCleanupDbs = config.Databases.Values
                .Where(p => p.CleanupMethod == CleanupMethod.AtStartup)
                .ToList();

            if (startupCleanupDbs.Count > 0)
            {
                _logger.LogInformation("Running startup cleanup for {Count} database(s)", startupCleanupDbs.Count);
                foreach (var item in startupCleanupDbs)
                {
                    _logger.LogDebug("Running cleanup for database: {Name}", item.Name);
                    var progress = new Progress<string>(p => _logger.LogDebug("Cleanup: {Message}", p));
                    await _maintenanceService.RunCleanupAsync(item, progress);
                }
            }

            _logger.LogDebug("Startup maintenance tasks completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during startup maintenance tasks");
        }
    }

    /// <summary>
    /// Creates and shows application windows based on configuration.
    /// </summary>
    private async Task CreateApplicationWindowsAsync()
    {
        var config = _configService.Configuration.Preferences;

        // Validate icon visibility - at least one must be visible
        if (config is { ShowTrayIcon: false, ShowTaskbarIcon: false })
        {
            _logger.LogCritical("Both tray icon and taskbar icon are disabled! Forcing tray icon to be visible for user access.");
            config.ShowTrayIcon = true;
            await _configService.SaveAsync();
        }

        // Create and show the tray icon window if enabled
        if (config.ShowTrayIcon)
        {
            var trayIconWindow = _serviceProvider.GetRequiredService<TrayIconWindow>();
            trayIconWindow.Show();
            _logger.LogDebug("Tray icon window created");
        }
        else
            _logger.LogDebug("Tray icon disabled in configuration");

        // ExplorerWindow ShowInTaskbar is set in ExplorerWindow constructor from config
    }
}
