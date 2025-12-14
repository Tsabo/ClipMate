using System.Collections.ObjectModel;
using System.IO;
using ClipMate.Core.Models.Configuration;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the MultipleDatabaseBackupDialog.
/// </summary>
public class MultipleDatabaseBackupViewModel : ObservableObject
{
    private bool _autoConfirmEnabled;
    private int _autoConfirmSeconds;
    private int _countdownSeconds;
    private bool _countdownVisible;
    private string _sharedBackupDirectory;
    private int _sharedBackupIntervalDays;

    public MultipleDatabaseBackupViewModel(IEnumerable<DatabaseConfiguration> configs,
        int globalBackupIntervalDays,
        int globalAutoConfirmSeconds)
    {
        DatabaseItems = new ObservableCollection<DatabaseBackupItem>(
            configs.Select(c => new DatabaseBackupItem(c)));

        _sharedBackupDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "ClipMate Backups");

        _sharedBackupIntervalDays = globalBackupIntervalDays;
        _autoConfirmEnabled = globalAutoConfirmSeconds > 0;
        _autoConfirmSeconds = globalAutoConfirmSeconds > 0
            ? globalAutoConfirmSeconds
            : 10;

        _countdownSeconds = 0;
        _countdownVisible = false;

        // Subscribe to selection changes for updating count
        foreach (var item in DatabaseItems)
        {
            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(DatabaseBackupItem.IsSelected))
                    OnPropertyChanged(nameof(SelectedCountText));
            };
        }
    }

    public ObservableCollection<DatabaseBackupItem> DatabaseItems { get; }

    public string SharedBackupDirectory
    {
        get => _sharedBackupDirectory;
        set => SetProperty(ref _sharedBackupDirectory, value);
    }

    public int SharedBackupIntervalDays
    {
        get => _sharedBackupIntervalDays;
        set => SetProperty(ref _sharedBackupIntervalDays, value);
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

    public string SelectedCountText
    {
        get
        {
            var count = DatabaseItems.Count(d => d.IsSelected);
            return count == 1
                ? "1 database"
                : $"{count} databases";
        }
    }

    public List<DatabaseConfiguration> GetSelectedDatabasesWithUpdatedSettings()
    {
        return DatabaseItems
            .Where(p => p.IsSelected)
            .Select(p => new DatabaseConfiguration
            {
                Name = p.OriginalConfig.Name,
                FilePath = p.OriginalConfig.FilePath,
                AutoLoad = p.OriginalConfig.AutoLoad,
                MultiUser = p.OriginalConfig.MultiUser,
                UserName = p.OriginalConfig.UserName,
                AllowBackup = p.OriginalConfig.AllowBackup,
                ReadOnly = p.OriginalConfig.ReadOnly,
                PurgeDays = p.OriginalConfig.PurgeDays,
                CleanupMethod = p.OriginalConfig.CleanupMethod,
                TempFileLocation = p.OriginalConfig.TempFileLocation,
                RemoteHost = p.OriginalConfig.RemoteHost,
                RemoteUserId = p.OriginalConfig.RemoteUserId,
                RemotePassword = p.OriginalConfig.RemotePassword,
                SetOfflineDailyAt = p.OriginalConfig.SetOfflineDailyAt,
                LastBackupDate = p.OriginalConfig.LastBackupDate,
                BackupDirectory = SharedBackupDirectory,
            })
            .ToList();
    }
}

/// <summary>
/// Represents a database item in the backup list.
/// </summary>
public class DatabaseBackupItem : ObservableObject
{
    private bool _isSelected = true;

    public DatabaseBackupItem(DatabaseConfiguration config)
    {
        OriginalConfig = config;
    }

    public DatabaseConfiguration OriginalConfig { get; }

    public string Name => OriginalConfig.Name;

    public string LastBackupInfo
    {
        get
        {
            if (OriginalConfig.LastBackupDate == null)
                return "Never backed up";

            var daysSince = (DateTime.Now - OriginalConfig.LastBackupDate.Value).Days;
            return daysSince switch
            {
                0 => "Last backup: Today",
                1 => "Last backup: Yesterday",
                var _ => $"Last backup: {daysSince} days ago ({OriginalConfig.LastBackupDate.Value:yyyy-MM-dd})",
            };
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
