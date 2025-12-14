using System.IO;
using System.Windows;
using ClipMate.App.ViewModels;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Dialogs;
using Microsoft.Extensions.Logging;

namespace ClipMate.App.Views.Dialogs;

/// <summary>
/// Three-page wizard for restoring a database from a backup ZIP file.
/// </summary>
public partial class DatabaseRestoreWizard
{
    private readonly ILogger<DatabaseRestoreWizard> _logger;
    private readonly IDatabaseMaintenanceService _maintenanceService;
    private readonly RestoreWizardViewModel _viewModel;

    public DatabaseRestoreWizard(DatabaseConfiguration databaseConfig,
        IDatabaseMaintenanceService maintenanceService,
        ILogger<DatabaseRestoreWizard> logger)
    {
        InitializeComponent();

        _maintenanceService = maintenanceService ?? throw new ArgumentNullException(nameof(maintenanceService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _viewModel = new RestoreWizardViewModel(databaseConfig);
        DataContext = _viewModel;
    }

    /// <summary>
    /// Gets whether the restore operation was successful.
    /// </summary>
    public bool RestoreSuccessful { get; private set; }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.CurrentPage > 0)
            _viewModel.CurrentPage--;
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        // Validate current page
        if (!ValidateCurrentPage())
            return;

        if (_viewModel.CurrentPage < 2)
            _viewModel.CurrentPage++;
    }

    private async void FinishButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Move to completion page
            _viewModel.CurrentPage = 2;
            _viewModel.IsRestoring = true;
            _viewModel.ProgressMessage = "Starting restore...";

            var progress = new Progress<string>(p => Dispatcher.Invoke(() => _viewModel.ProgressMessage = p));

            // Perform restore
            await _maintenanceService.RestoreDatabaseAsync(
                _viewModel.BackupZipPath,
                _viewModel.DatabaseConfig,
                progress);

            _viewModel.ProgressMessage = "Restore completed successfully!";
            _viewModel.IsRestoring = false;
            RestoreSuccessful = true;

            _logger.LogInformation("Database restore completed: {Database}", _viewModel.DatabaseConfig.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database restore failed");
            _viewModel.IsRestoring = false;
            _viewModel.ProgressMessage = $"Restore failed: {ex.Message}";

            DXMessageBox.Show(
                this,
                $"Database restore failed:\n\n{ex.Message}",
                "Restore Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            RestoreSuccessful = false;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsRestoring)
        {
            var result = DXMessageBox.Show(
                this,
                "Restore is in progress. Are you sure you want to cancel?",
                "Confirm Cancel",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;
        }

        DialogResult = false;
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = RestoreSuccessful;
        Close();
    }

    private void BrowseBackupButton_Click(object sender, RoutedEventArgs e)
    {
        using var openDialog = new DXOpenFileDialog();

        openDialog.Title = "Select Backup ZIP File";
        openDialog.Filter = "ZIP files (*.zip)|*.zip|All files (*.*)|*.*";
        openDialog.FilterIndex = 1;

        if (openDialog.ShowDialog() == true)
            _viewModel.BackupZipPath = openDialog.FileName;
    }

    private bool ValidateCurrentPage()
    {
        switch (_viewModel.CurrentPage)
        {
            case 0: // Welcome page - no validation needed
                return true;

            case 1: // Confirmation page - validate backup file selection
                if (string.IsNullOrWhiteSpace(_viewModel.BackupZipPath))
                {
                    DXMessageBox.Show(
                        this,
                        "Please select a backup ZIP file to restore from.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return false;
                }

                if (!File.Exists(_viewModel.BackupZipPath))
                {
                    DXMessageBox.Show(
                        this,
                        $"The selected backup file does not exist:\n{_viewModel.BackupZipPath}",
                        "File Not Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    return false;
                }

                return true;

            default:
                return true;
        }
    }
}
