using System.IO;
using System.Windows;
using System.Windows.Threading;
using ClipMate.App.ViewModels;
using ClipMate.Core.Models.Configuration;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Dialogs;

namespace ClipMate.App.Views.Dialogs;

/// <summary>
/// Dialog for backing up multiple databases at once.
/// </summary>
public partial class MultipleDatabaseBackupDialog
{
    private readonly DispatcherTimer? _countdownTimer;
    private readonly MultipleDatabaseBackupViewModel _viewModel;
    private bool _userInteracted;

    /// <summary>
    /// Initializes a new instance for the specified databases.
    /// </summary>
    /// <param name="databaseConfigs">The list of database configurations to backup.</param>
    /// <param name="globalBackupIntervalDays">Global backup interval from preferences.</param>
    /// <param name="globalAutoConfirmSeconds">Global auto-confirm seconds from preferences.</param>
    public MultipleDatabaseBackupDialog(IEnumerable<DatabaseConfiguration> databaseConfigs,
        int globalBackupIntervalDays,
        int globalAutoConfirmSeconds)
    {
        InitializeComponent();

        _viewModel = new MultipleDatabaseBackupViewModel(
            databaseConfigs,
            globalBackupIntervalDays,
            globalAutoConfirmSeconds);

        DataContext = _viewModel;

        // Setup countdown timer if auto-confirm is enabled
        if (_viewModel is not { AutoConfirmEnabled: true, AutoConfirmSeconds: > 0 })
            return;

        _viewModel.CountdownSeconds = _viewModel.AutoConfirmSeconds;
        _viewModel.CountdownVisible = true;

        _countdownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };

        _countdownTimer.Tick += CountdownTimer_Tick;
        _countdownTimer.Start();
    }

    /// <summary>
    /// Gets the list of databases selected for backup with updated configurations.
    /// </summary>
    public List<DatabaseConfiguration> SelectedDatabases { get; private set; } = new();

    /// <summary>
    /// Gets whether backups should proceed.
    /// </summary>
    public bool ShouldBackup { get; private set; }

    private void CountdownTimer_Tick(object? sender, EventArgs e)
    {
        if (_userInteracted)
        {
            // User interacted, stop countdown
            _countdownTimer?.Stop();
            _viewModel.CountdownVisible = false;
            return;
        }

        _viewModel.CountdownSeconds--;

        if (_viewModel.CountdownSeconds > 0)
            return;

        _countdownTimer?.Stop();
        // Auto-confirm the dialog
        PerformBackup();
    }

    private void OnUserInteraction(object sender, RoutedEventArgs e)
    {
        _userInteracted = true;
        _viewModel.CountdownVisible = false;
    }

    private void SelectAllButton_Click(object sender, RoutedEventArgs e)
    {
        _userInteracted = true;
        foreach (var item in _viewModel.DatabaseItems)
            item.IsSelected = true;
    }

    private void DeselectAllButton_Click(object sender, RoutedEventArgs e)
    {
        _userInteracted = true;
        foreach (var item in _viewModel.DatabaseItems)
            item.IsSelected = false;
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        _userInteracted = true;

        using var folderDialog = new DXFolderBrowserDialog();

        folderDialog.Description = "Select Backup Directory";
        folderDialog.SelectedPath = string.IsNullOrWhiteSpace(_viewModel.SharedBackupDirectory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            : Environment.ExpandEnvironmentVariables(_viewModel.SharedBackupDirectory);

        folderDialog.ShowNewFolderButton = true;

        if (folderDialog.ShowDialog() == true)
            _viewModel.SharedBackupDirectory = folderDialog.SelectedPath;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        _userInteracted = true;
        PerformBackup();
    }

    private void PerformBackup()
    {
        // Check if any databases are selected
        if (_viewModel.DatabaseItems.All(d => !d.IsSelected))
        {
            DXMessageBox.Show(
                this,
                "Please select at least one database to backup.",
                "No Selection",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        // Validate backup directory
        if (string.IsNullOrWhiteSpace(_viewModel.SharedBackupDirectory))
        {
            DXMessageBox.Show(
                this,
                "Please specify a backup directory.",
                "Validation Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        var expandedPath = Environment.ExpandEnvironmentVariables(_viewModel.SharedBackupDirectory);

        // Check if directory exists
        if (!Directory.Exists(expandedPath))
        {
            var result = DXMessageBox.Show(
                this,
                $"The backup directory does not exist:\n{expandedPath}\n\nWould you like to create it?",
                "Directory Not Found",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
                return;

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    Directory.CreateDirectory(expandedPath);
                }
                catch (Exception ex)
                {
                    DXMessageBox.Show(
                        this,
                        $"Failed to create backup directory:\n{ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    return;
                }
            }
        }

        // Test write permissions
        if (Directory.Exists(expandedPath))
        {
            var testFile = Path.Combine(expandedPath, $".clipmate_backup_test_{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
            }
            catch (UnauthorizedAccessException)
            {
                DXMessageBox.Show(
                    this,
                    $"You do not have write permissions for the backup directory:\n{expandedPath}",
                    "Permission Denied",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }
        }

        // Stop timer if running
        _countdownTimer?.Stop();

        // Collect selected databases with updated configurations
        SelectedDatabases = _viewModel.GetSelectedDatabasesWithUpdatedSettings();
        ShouldBackup = SelectedDatabases.Count > 0;

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _userInteracted = true;
        _countdownTimer?.Stop();
        ShouldBackup = false;
        DialogResult = false;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _countdownTimer?.Stop();
        base.OnClosed(e);
    }
}
