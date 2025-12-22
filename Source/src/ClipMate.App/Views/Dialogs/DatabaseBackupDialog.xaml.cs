using System.IO;
using System.Windows.Threading;
using ClipMate.App.ViewModels;
using ClipMate.Core.Models.Configuration;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Dialogs;

namespace ClipMate.App.Views.Dialogs;

/// <summary>
/// Dialog for configuring and initiating database backup.
/// </summary>
public partial class DatabaseBackupDialog
{
    private readonly DispatcherTimer? _countdownTimer;
    private readonly DatabaseBackupViewModel _viewModel;
    private bool _userInteracted;

    /// <summary>
    /// Initializes a new instance for the specified database.
    /// </summary>
    /// <param name="databaseConfig">The database configuration to backup.</param>
    /// <param name="globalBackupIntervalDays">Global backup interval from preferences.</param>
    /// <param name="globalAutoConfirmSeconds">Global auto-confirm seconds from preferences.</param>
    public DatabaseBackupDialog(DatabaseConfiguration databaseConfig,
        int globalBackupIntervalDays,
        int globalAutoConfirmSeconds)
    {
        InitializeComponent();

        _viewModel = new DatabaseBackupViewModel(databaseConfig, globalBackupIntervalDays, globalAutoConfirmSeconds);
        DataContext = _viewModel;

        // Setup countdown timer if auto-confirm is enabled
        if (_viewModel is { AutoConfirmEnabled: true, AutoConfirmSeconds: > 0 })
        {
            _viewModel.CountdownSeconds = _viewModel.AutoConfirmSeconds;
            _viewModel.CountdownVisible = true;

            _countdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1),
            };

            _countdownTimer.Tick += CountdownTimer_Tick;
            _countdownTimer.Start();
        }

        Loaded += OnLoaded;
    }

    /// <summary>
    /// Gets the updated database configuration with new backup settings.
    /// </summary>
    public DatabaseConfiguration? UpdatedConfiguration { get; private set; }

    /// <summary>
    /// Gets whether the backup should proceed.
    /// </summary>
    public bool ShouldBackup { get; private set; }

    private void OnLoaded(object sender, RoutedEventArgs e) => Title = $"ClipMate Database [{_viewModel.DatabaseName}] Backup";

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

        if (_viewModel.CountdownSeconds <= 0)
        {
            _countdownTimer?.Stop();
            // Auto-confirm the dialog
            PerformBackup();
        }
    }

    private void OnUserInteraction(object sender, RoutedEventArgs e)
    {
        _userInteracted = true;
        _viewModel.CountdownVisible = false;
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        _userInteracted = true;

        using var folderDialog = new DXFolderBrowserDialog();

        folderDialog.Description = "Select Backup Directory";
        folderDialog.SelectedPath = string.IsNullOrWhiteSpace(_viewModel.BackupDirectory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            : Environment.ExpandEnvironmentVariables(_viewModel.BackupDirectory);

        folderDialog.ShowNewFolderButton = true;

        if (folderDialog.ShowDialog() == true)
            _viewModel.BackupDirectory = folderDialog.SelectedPath;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        _userInteracted = true;
        PerformBackup();
    }

    private void PerformBackup()
    {
        // Validate backup directory
        if (string.IsNullOrWhiteSpace(_viewModel.BackupDirectory))
        {
            DXMessageBox.Show(
                this,
                "Please specify a backup directory.",
                "Validation Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        var expandedPath = Environment.ExpandEnvironmentVariables(_viewModel.BackupDirectory);

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

        // Update configuration with new settings
        UpdatedConfiguration = _viewModel.ToConfiguration();
        ShouldBackup = true;
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
