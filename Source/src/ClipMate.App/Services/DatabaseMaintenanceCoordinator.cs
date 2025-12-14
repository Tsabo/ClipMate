using System.Windows;
using ClipMate.App.Views.Dialogs;
using ClipMate.Core.Events;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using DevExpress.Xpf.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Application = System.Windows.Application;

namespace ClipMate.App.Services;

/// <summary>
/// Coordinates database maintenance operations by handling database-related events
/// and invoking the appropriate dialogs/services regardless of which window is active.
/// </summary>
public class DatabaseMaintenanceCoordinator :
    IRecipient<BackupDatabaseRequestedEvent>,
    IRecipient<RestoreDatabaseRequestedEvent>,
    IRecipient<EmptyTrashRequestedEvent>,
    IRecipient<SimpleRepairRequestedEvent>,
    IRecipient<ComprehensiveRepairRequestedEvent>
{
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<DatabaseMaintenanceCoordinator> _logger;
    private readonly IDatabaseMaintenanceService _maintenanceService;
    private readonly IMessenger _messenger;
    private readonly IServiceProvider _serviceProvider;

    public DatabaseMaintenanceCoordinator(IServiceProvider serviceProvider,
        IDatabaseMaintenanceService maintenanceService,
        IConfigurationService configurationService,
        IMessenger messenger,
        ILogger<DatabaseMaintenanceCoordinator> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _maintenanceService = maintenanceService ?? throw new ArgumentNullException(nameof(maintenanceService));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Register for all database maintenance events
        _messenger.Register<BackupDatabaseRequestedEvent>(this);
        _messenger.Register<RestoreDatabaseRequestedEvent>(this);
        _messenger.Register<EmptyTrashRequestedEvent>(this);
        _messenger.Register<SimpleRepairRequestedEvent>(this);
        _messenger.Register<ComprehensiveRepairRequestedEvent>(this);

        _logger.LogInformation("DatabaseMaintenanceCoordinator initialized and registered for events");
    }

    /// <summary>
    /// Handles BackupDatabaseRequestedEvent from menu.
    /// </summary>
    public void Receive(BackupDatabaseRequestedEvent message)
    {
        Application.Current.Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                var currentDb = _configurationService.Configuration.Databases.Values.FirstOrDefault(db => db.Name == "My Clips");

                if (currentDb == null)
                {
                    ThemedMessageBox.Show("No database is currently loaded.", "Backup Database", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var globalBackupInterval = _configurationService.Configuration.Preferences.BackupIntervalDays;
                var globalAutoConfirm = _configurationService.Configuration.Preferences.AutoConfirmBackupSeconds;

                var dialog = new DatabaseBackupDialog(currentDb, globalBackupInterval, globalAutoConfirm)
                {
                    Owner = Application.Current.MainWindow,
                };

                _logger.LogInformation("Showing backup dialog for database: {Name}", currentDb.Name);

                if (dialog.ShowDialog() == true && dialog is { ShouldBackup: true, UpdatedConfiguration: not null })
                {
                    // Perform the backup
                    var backupPath = await _maintenanceService.BackupDatabaseAsync(
                        currentDb,
                        dialog.UpdatedConfiguration.BackupDirectory);

                    // Update configuration
                    currentDb.LastBackupDate = DateTime.Now;
                    currentDb.BackupDirectory = dialog.UpdatedConfiguration.BackupDirectory;

                    await _configurationService.SaveAsync();

                    _logger.LogInformation("Backup completed: {Path}", backupPath);
                    ThemedMessageBox.Show($"Database backup completed successfully!\n\nBackup saved to:\n{backupPath}",
                        "Backup Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error backing up database");
                ThemedMessageBox.Show($"Error backing up database:\n{ex.Message}", "Backup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });
    }

    /// <summary>
    /// Handles ComprehensiveRepairRequestedEvent from menu.
    /// </summary>
    public void Receive(ComprehensiveRepairRequestedEvent message)
    {
        Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                var result = ThemedMessageBox.Show(
                    "This will backup, export, and rebuild the database.\n\nThis process may take several minutes.\n\nContinue?",
                    "Comprehensive Repair",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    return;

                var currentDb = _configurationService.Configuration.Databases.Values.FirstOrDefault(db => db.Name == "My Clips");

                if (currentDb == null)
                {
                    ThemedMessageBox.Show("No database is currently loaded.", "Comprehensive Repair", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var progress = new Progress<string>(p => _logger.LogInformation("Comprehensive Repair: {Message}", p));
                await _maintenanceService.ComprehensiveRepairDatabaseAsync(currentDb, progress);

                ThemedMessageBox.Show("Comprehensive database repair completed successfully.", "Repair Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                _logger.LogInformation("Comprehensive database repair completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in comprehensive repair");
                ThemedMessageBox.Show($"Error in comprehensive repair:\n{ex.Message}", "Repair Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });
    }

    /// <summary>
    /// Handles EmptyTrashRequestedEvent from menu.
    /// </summary>
    public void Receive(EmptyTrashRequestedEvent message)
    {
        Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                var result = ThemedMessageBox.Show(
                    "Are you sure you want to permanently delete all items in the trash?\n\nThis action cannot be undone.",
                    "Empty Trash",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    return;

                var currentDb = _configurationService.Configuration.Databases.Values.FirstOrDefault(db => db.Name == "My Clips");

                if (currentDb == null)
                {
                    ThemedMessageBox.Show("No database is currently loaded.", "Empty Trash", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var progress = new Progress<string>(p => _logger.LogInformation("Empty Trash: {Message}", p));
                var count = await _maintenanceService.EmptyTrashAsync(currentDb, progress);

                ThemedMessageBox.Show($"Successfully deleted {count} item(s) from trash.", "Empty Trash Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                _logger.LogInformation("Empty trash completed: {Count} items deleted", count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error emptying trash");
                ThemedMessageBox.Show($"Error emptying trash:\n{ex.Message}", "Empty Trash Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });
    }

    /// <summary>
    /// Handles RestoreDatabaseRequestedEvent from menu.
    /// </summary>
    public void Receive(RestoreDatabaseRequestedEvent message)
    {
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                var wizard = _serviceProvider.GetRequiredService<DatabaseRestoreWizard>();
                var activeWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                                   ?? Application.Current.MainWindow;

                wizard.Owner = activeWindow;
                wizard.ShowDialog();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing restore wizard");
                ThemedMessageBox.Show($"Error opening restore wizard:\n{ex.Message}", "Restore Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });
    }

    /// <summary>
    /// Handles SimpleRepairRequestedEvent from menu.
    /// </summary>
    public void Receive(SimpleRepairRequestedEvent message)
    {
        Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                var result = ThemedMessageBox.Show(
                    "This will run VACUUM on the database to reclaim space and optimize performance.\n\nContinue?",
                    "Simple Repair",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;

                var currentDb = _configurationService.Configuration.Databases.Values.FirstOrDefault(db => db.Name == "My Clips");

                if (currentDb == null)
                {
                    ThemedMessageBox.Show("No database is currently loaded.", "Simple Repair", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var progress = new Progress<string>(p => _logger.LogInformation("Repair: {Message}", p));
                await _maintenanceService.RepairDatabaseAsync(currentDb, progress);

                ThemedMessageBox.Show("Database repair completed successfully.", "Repair Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                _logger.LogInformation("Simple database repair completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error repairing database");
                ThemedMessageBox.Show($"Error repairing database:\n{ex.Message}", "Repair Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });
    }
}
