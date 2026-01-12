using ClipMate.App.Views.Dialogs;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using Microsoft.Extensions.Logging;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;

namespace ClipMate.App.Services;

/// <summary>
/// Orchestrates database backup prompting and execution during application startup.
/// Handles both single and multiple database backup workflows.
/// </summary>
public class BackupOrchestrationService
{
    private readonly IConfigurationService _configService;
    private readonly ILogger<BackupOrchestrationService> _logger;
    private readonly IDatabaseMaintenanceService _maintenanceService;

    public BackupOrchestrationService(IConfigurationService configService,
        IDatabaseMaintenanceService maintenanceService,
        ILogger<BackupOrchestrationService> logger)
    {
        _configService = configService;
        _maintenanceService = maintenanceService;
        _logger = logger;
    }

    /// <summary>
    /// Checks if any databases are due for backup and prompts the user.
    /// </summary>
    public async Task CheckAndPromptForBackupsAsync()
    {
        try
        {
            var config = _configService.Configuration;

            // Check if backup interval is disabled globally
            if (config.Preferences.BackupIntervalDays is 0 or >= 9999)
            {
                _logger.LogDebug("Automatic backups disabled (interval: {Days} days)", config.Preferences.BackupIntervalDays);
                return;
            }

            // Get list of databases that need backup
            var databasesDue = await _maintenanceService.CheckBackupDueAsync(config.Databases.Values);

            if (databasesDue.Count == 0)
            {
                _logger.LogDebug("No databases due for backup");
                return;
            }

            _logger.LogInformation("Found {Count} database(s) due for backup", databasesDue.Count);

            // Filter out databases that were recently prompted (within 3 days)
            const int promptSnoozesDays = 3;
            var now = DateTime.UtcNow;
            var databasesToPrompt = databasesDue
                .Where(p => p.LastBackupPromptDate == null ||
                            (now - p.LastBackupPromptDate.Value).TotalDays >= promptSnoozesDays)
                .ToList();

            if (databasesToPrompt.Count == 0)
            {
                _logger.LogDebug("All databases due for backup were recently prompted (within {Days} days)", promptSnoozesDays);
                return;
            }

            // Show backup dialog(s) based on count
            if (databasesToPrompt.Count == 1)
                await HandleSingleDatabaseBackupAsync(databasesToPrompt[0], config);
            else
                await HandleMultipleDatabaseBackupsAsync(databasesToPrompt, config);

            // Save configuration with updated backup dates
            await _configService.SaveAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for backup-due databases");
        }
    }

    /// <summary>
    /// Handles backup workflow for a single database.
    /// </summary>
    private async Task HandleSingleDatabaseBackupAsync(DatabaseConfiguration dbConfig, ClipMateConfiguration config)
    {
        var dialog = new DatabaseBackupDialog(
            dbConfig,
            config.Preferences.BackupIntervalDays,
            config.Preferences.AutoConfirmBackupSeconds)
        {
            Owner = Application.Current.GetDialogOwner(),
        };

        // Record that we prompted the user
        dbConfig.LastBackupPromptDate = DateTime.UtcNow;

        if (dialog.ShowDialog() == true && dialog is { ShouldBackup: true, UpdatedConfiguration: not null })
            await PerformBackupAsync(dbConfig, dialog.UpdatedConfiguration);
        else
            await _configService.SaveAsync(); // Save the prompt date even if cancelled
    }

    /// <summary>
    /// Handles backup workflow for multiple databases.
    /// </summary>
    private async Task HandleMultipleDatabaseBackupsAsync(List<DatabaseConfiguration> databasesToPrompt, ClipMateConfiguration config)
    {
        var dialog = new MultipleDatabaseBackupDialog(
            databasesToPrompt,
            config.Preferences.BackupIntervalDays,
            config.Preferences.AutoConfirmBackupSeconds)
        {
            Owner = Application.Current.GetDialogOwner(),
        };

        // Record that we prompted the user for all databases
        foreach (var item in databasesToPrompt)
            item.LastBackupPromptDate = DateTime.UtcNow;

        if (dialog.ShowDialog() == true && dialog.ShouldBackup && dialog.SelectedDatabases.Count != 0)
        {
            foreach (var item in dialog.SelectedDatabases)
                await PerformBackupAsync(item, item);
        }
        else
            await _configService.SaveAsync(); // Save the prompt dates even if cancelled
    }

    /// <summary>
    /// Performs a database backup operation.
    /// </summary>
    private async Task PerformBackupAsync(DatabaseConfiguration dbConfig, DatabaseConfiguration updatedConfig)
    {
        try
        {
            var backupPath = await _maintenanceService.BackupDatabaseAsync(
                dbConfig,
                updatedConfig.BackupDirectory);

            // Update last backup date
            dbConfig.LastBackupDate = DateTime.Now;
            dbConfig.BackupDirectory = updatedConfig.BackupDirectory;

            _logger.LogInformation("Backup completed: {Path}", backupPath);

            // Show success notification
            MessageBox.Show(
                $"Database backup completed successfully!\n\nBackup saved to:\n{backupPath}",
                "Backup Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup failed for database: {Database}", dbConfig.Name);

            MessageBox.Show(
                $"Database backup failed:\n\n{ex.Message}",
                "Backup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Runs shutdown maintenance tasks: CleanupMethod.AtShutdown triggers.
    /// Executes cleanup for databases configured with AtShutdown before application terminates.
    /// </summary>
    public async Task RunShutdownMaintenanceTasksAsync()
    {
        try
        {
            var config = _configService.Configuration;

            // Run CleanupMethod.AtShutdown for databases configured with it.
            // This permanently deletes clips from Trashcan based on PurgeDays setting.
            var shutdownCleanupDbs = config.Databases.Values
                .Where(p => p.CleanupMethod == CleanupMethod.AtShutdown)
                .ToList();

            if (shutdownCleanupDbs.Count > 0)
            {
                _logger.LogInformation("Running shutdown cleanup for {Count} database(s)", shutdownCleanupDbs.Count);
                foreach (var item in shutdownCleanupDbs)
                {
                    _logger.LogDebug("Running cleanup for database: {Name}", item.Name);
                    var progress = new Progress<string>(message => _logger.LogDebug("Cleanup: {Message}", message));
                    await _maintenanceService.RunCleanupAsync(item, progress);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during shutdown maintenance tasks");
        }
    }
}
