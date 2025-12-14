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
        set => SetProperty(ref _countdownSeconds, value);
    }

    public bool CountdownVisible
    {
        get => _countdownVisible;
        set => SetProperty(ref _countdownVisible, value);
    }

    public string BackupFileName =>
        $"ClipMate_DB_{DatabaseName}_{DateTime.Now:yyyy-MM-dd}.zip";

    public DatabaseConfiguration ToConfiguration()
    {
        // Create a copy of the original configuration with updated backup settings
        return new DatabaseConfiguration
        {
            Name = _originalConfig.Name,
            FilePath = _originalConfig.FilePath,
            AutoLoad = _originalConfig.AutoLoad,
            MultiUser = _originalConfig.MultiUser,
            UserName = _originalConfig.UserName,
            AllowBackup = _originalConfig.AllowBackup,
            ReadOnly = _originalConfig.ReadOnly,
            PurgeDays = _originalConfig.PurgeDays,
            CleanupMethod = _originalConfig.CleanupMethod,
            TempFileLocation = _originalConfig.TempFileLocation,
            RemoteHost = _originalConfig.RemoteHost,
            RemoteUserId = _originalConfig.RemoteUserId,
            RemotePassword = _originalConfig.RemotePassword,
            SetOfflineDailyAt = _originalConfig.SetOfflineDailyAt,
            LastBackupDate = _originalConfig.LastBackupDate,
            BackupDirectory = BackupDirectory,
        };
    }
}
