using System.IO;
using ClipMate.Core.Models.Configuration;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the DatabaseBackupDialog.
/// </summary>
public class DatabaseBackupViewModel : ObservableObject
{
    private readonly DatabaseConfiguration _originalConfig;
    private bool _autoConfirmEnabled;
    private int _autoConfirmSeconds;
    private string _backupDirectory;
    private int _backupIntervalDays;
    private int _countdownSeconds;
    private bool _countdownVisible;

    public DatabaseBackupViewModel(DatabaseConfiguration config,
        int globalBackupIntervalDays,
        int globalAutoConfirmSeconds)
    {
        _originalConfig = config;
        DatabaseName = config.Name;

        // Use database-specific backup directory if set, otherwise use default
        _backupDirectory = string.IsNullOrWhiteSpace(config.BackupDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ClipMate Backups")
            : config.BackupDirectory;

        _backupIntervalDays = globalBackupIntervalDays;
        _autoConfirmEnabled = globalAutoConfirmSeconds > 0;
        _autoConfirmSeconds = globalAutoConfirmSeconds > 0
            ? globalAutoConfirmSeconds
            : 10;

        _countdownSeconds = 0;
        _countdownVisible = false;
    }

    public string DatabaseName { get; }

    public string HeaderText => $"Configure backup settings for database: {DatabaseName}";

    public string BackupDirectory
    {
        get => _backupDirectory;
        set => SetProperty(ref _backupDirectory, value);
    }

    public int BackupIntervalDays
    {
        get => _backupIntervalDays;
        set => SetProperty(ref _backupIntervalDays, value);
    }

    public bool AutoConfirmEnabled
    {
        get => _autoConfirmEnabled;
        set
        {
            if (SetProperty(ref _autoConfirmEnabled, value))
                OnPropertyChanged(nameof(CountdownVisible));
        }
    }

    public int AutoConfirmSeconds
    {
        get => _autoConfirmSeconds;
        set => SetProperty(ref _autoConfirmSeconds, value);
    }

    public int CountdownSeconds
    {
        get => _countdownSeconds;
        set
        {
            if (SetProperty(ref _countdownSeconds, value))
                OnPropertyChanged(nameof(OkButtonText));
        }
    }

    public bool CountdownVisible
    {
        get => _countdownVisible;
        set
        {
            if (SetProperty(ref _countdownVisible, value))
                OnPropertyChanged(nameof(OkButtonText));
        }
    }

    public string OkButtonText => CountdownVisible
        ? $"OK ({CountdownSeconds})"
        : "OK";

    public string BackupFileName =>
        $"ClipMate_DB_{DatabaseName}_{DateTime.Now:yyyy-MM-dd}.zip";

    public DatabaseConfiguration ToConfiguration()
    {
        // Every setting of the original, with only the backup folder updated
        var configuration = _originalConfig.Clone();
        configuration.BackupDirectory = BackupDirectory;
        return configuration;
    }
}
