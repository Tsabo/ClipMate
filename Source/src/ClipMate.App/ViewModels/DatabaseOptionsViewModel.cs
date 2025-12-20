using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using ClipMate.App.Views.Dialogs;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Application = System.Windows.Application;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the Database options tab.
/// </summary>
public partial class DatabaseOptionsViewModel : ObservableObject
{
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<DatabaseOptionsViewModel> _logger;

    [ObservableProperty]
    private int _autoConfirmBackupSeconds;

    [ObservableProperty]
    private int _backupIntervalDays;

    [ObservableProperty]
    private ObservableCollection<DatabaseConfigurationViewModel> _databases = [];

    [ObservableProperty]
    private DatabaseConfigurationViewModel? _selectedDatabase;

    [ObservableProperty]
    private int _selectedDatabaseIndex = -1;

    public DatabaseOptionsViewModel(IConfigurationService configurationService,
        ILogger<DatabaseOptionsViewModel> logger)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Loads database configuration.
    /// </summary>
    public async Task LoadAsync()
    {
        await Task.Run(() =>
        {
            var config = _configurationService.Configuration;

            // Convert Dictionary to ObservableCollection with ViewModels, preserving keys
            Databases = new ObservableCollection<DatabaseConfigurationViewModel>(
                config.Databases.Select(p => new DatabaseConfigurationViewModel(p.Key, p.Value)));

            // Load global backup settings
            BackupIntervalDays = config.Preferences.BackupIntervalDays;
            AutoConfirmBackupSeconds = config.Preferences.AutoConfirmBackupSeconds;

            _logger.LogInformation("Database configuration loaded. {Count} databases found: {DatabaseKeys}",
                Databases.Count,
                string.Join(", ", config.Databases.Keys));
        });
    }

    /// <summary>
    /// Saves database configuration.
    /// </summary>
    public async Task SaveAsync()
    {
        await Task.Run(() =>
        {
            var config = _configurationService.Configuration;

            // Convert ObservableCollection back to Dictionary, preserving original keys
            config.Databases = Databases.ToDictionary(p => p.DatabaseKey, x => x.Configuration);

            // Save global backup settings
            config.Preferences.BackupIntervalDays = BackupIntervalDays;
            config.Preferences.AutoConfirmBackupSeconds = AutoConfirmBackupSeconds;

            _logger.LogDebug("Database configuration saved. {Count} databases", Databases.Count);
        });
    }

    /// <summary>
    /// Adds a new database configuration.
    /// </summary>
    [RelayCommand]
    private void AddDatabase()
    {
        var dialog = new DatabaseEditDialog
        {
            Owner = Application.Current.GetDialogOwner(),
        };

        if (dialog.ShowDialog() != true || dialog.DatabaseConfig == null)
            return;

        // Generate a unique GUID-based key for the new database
        var guid = Guid.NewGuid().ToString("N");
        var key = $"db_{guid[..8]}";
        Databases.Add(new DatabaseConfigurationViewModel(key, dialog.DatabaseConfig));
        _logger.LogDebug("Added database: {Name} with key {Key}", dialog.DatabaseConfig.Name, key);
    }

    /// <summary>
    /// Edits the selected database configuration.
    /// </summary>
    [RelayCommand]
    private void EditDatabase()
    {
        if (SelectedDatabase == null)
        {
            _logger.LogWarning("No database selected for editing");
            return;
        }

        var dialog = new DatabaseEditDialog(SelectedDatabase.Configuration)
        {
            Owner = Application.Current.GetDialogOwner(),
        };

        if (dialog.ShowDialog() != true || dialog.DatabaseConfig == null)
            return;

        var index = Databases.IndexOf(SelectedDatabase);
        if (index < 0)
            return;

        // Preserve the original key when updating
        var originalKey = SelectedDatabase.DatabaseKey;
        Databases[index] = new DatabaseConfigurationViewModel(originalKey, dialog.DatabaseConfig);
        SelectedDatabase = Databases[index];
        _logger.LogDebug("Edited database: {Name}", dialog.DatabaseConfig.Name);
    }

    /// <summary>
    /// Deletes the selected database configuration.
    /// </summary>
    [RelayCommand]
    private void DeleteDatabase()
    {
        if (SelectedDatabase == null)
        {
            _logger.LogWarning("No database selected for deletion");
            return;
        }

        var dbName = SelectedDatabase.Name;
        Databases.Remove(SelectedDatabase);
        _logger.LogDebug("Deleted database: {Name}", dbName);
    }

    /// <summary>
    /// Opens the selected database folder in Windows Explorer.
    /// </summary>
    [RelayCommand]
    private void OpenFolder()
    {
        if (SelectedDatabase == null)
        {
            _logger.LogWarning("No database selected to open folder");
            return;
        }

        var directory = Path.GetDirectoryName(SelectedDatabase.FilePath);

        if (string.IsNullOrWhiteSpace(directory))
        {
            _logger.LogWarning("Database directory is not set: {Name}", SelectedDatabase.Name);
            return;
        }

        try
        {
            if (Directory.Exists(directory))
            {
                Process.Start("explorer.exe", directory);
                _logger.LogDebug("Opened folder: {Directory}", directory);
            }
            else
                _logger.LogWarning("Database directory does not exist: {Directory}", directory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open folder: {Directory}", directory);
        }
    }

    /// <summary>
    /// Analyzes the database.
    /// </summary>
    [RelayCommand]
    private async Task AnalyzeDatabaseAsync()
    {
        _logger.LogDebug("Analyzing database");
        // TODO: Implement database analysis
        await Task.CompletedTask;
    }
}
