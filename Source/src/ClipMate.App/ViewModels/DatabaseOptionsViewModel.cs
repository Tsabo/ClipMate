using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using ClipMate.App.Views.Dialogs;
using ClipMate.Core.Events;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Application = System.Windows.Application;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the Database options tab.
/// </summary>
public partial class DatabaseOptionsViewModel : ObservableObject
{
    private readonly IClipMate7ImportService _clipMate7ImportService;
    private readonly IConfigurationService _configurationService;
    private readonly IDatabaseIdService _databaseIdService;
    private readonly IDatabaseManager _databaseManager;
    private readonly ILogger<DatabaseOptionsViewModel> _logger;
    private readonly IMessenger _messenger;
    private readonly ISharedDatabaseService _sharedDatabaseService;

    /// <summary>
    /// The database definitions as they were when the configuration was loaded or last saved.
    /// </summary>
    private Dictionary<string, DatabaseConfiguration> _savedDatabases = [];

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
        IClipMate7ImportService clipMate7ImportService,
        IDatabaseManager databaseManager,
        IMessenger messenger,
        ILogger<DatabaseOptionsViewModel> logger,
        IDatabaseIdService databaseIdService,
        ISharedDatabaseService sharedDatabaseService)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _clipMate7ImportService = clipMate7ImportService ?? throw new ArgumentNullException(nameof(clipMate7ImportService));
        _databaseManager = databaseManager ?? throw new ArgumentNullException(nameof(databaseManager));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _databaseIdService = databaseIdService ?? throw new ArgumentNullException(nameof(databaseIdService));
        _sharedDatabaseService = sharedDatabaseService ?? throw new ArgumentNullException(nameof(sharedDatabaseService));
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

            _savedDatabases = new Dictionary<string, DatabaseConfiguration>(config.Databases);

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
        var current = Databases.ToDictionary(p => p.DatabaseKey, p => p.Configuration);
        var removedKeys = _savedDatabases.Keys.Where(p => !current.ContainsKey(p)).ToList();
        var addedKeys = current.Where(p => p.Value.AutoLoad && !_savedDatabases.ContainsKey(p.Key)).Select(p => p.Key).ToList();

        // Edit replaces the configuration object, so a reference change means the definition was edited
        var changed = current.Count != _savedDatabases.Count ||
                      current.Any(p => !_savedDatabases.TryGetValue(p.Key, out var saved) || !ReferenceEquals(saved, p.Value));

        // Unload removed databases while their definitions are still in the configuration (unloading looks them up by key)
        foreach (var item in removedKeys)
        {
            if (_databaseManager.UnloadDatabase(item))
                _logger.LogInformation("Unloaded removed database: {Key}", item);
        }

        await Task.Run(() =>
        {
            var config = _configurationService.Configuration;

            // Convert ObservableCollection back to Dictionary, preserving original keys
            config.Databases = current;

            // Save global backup settings
            config.Preferences.BackupIntervalDays = BackupIntervalDays;
            config.Preferences.AutoConfirmBackupSeconds = AutoConfirmBackupSeconds;

            _logger.LogDebug("Database configuration saved. {Count} databases", Databases.Count);
        });

        _savedDatabases = new Dictionary<string, DatabaseConfiguration>(current);

        // Databases are otherwise only loaded at startup, so load auto-load databases added in this session (via Add or Import ClipMate 7)
        foreach (var item in addedKeys)
        {
            _logger.LogInformation("Activating newly added database: {Key}", item);
            _messenger.Send(new ActivateDatabaseRequestedEvent(item));
        }

        if (changed)
            _messenger.Send(new RefreshCollectionTreeRequestedEvent());
    }

    /// <summary>
    /// Adds a new database configuration.
    /// </summary>
    [RelayCommand]
    private void AddDatabase()
    {
        var dialog = new DatabaseEditDialog(CreateSharingContext(null))
        {
            Owner = Application.Current.GetDialogOwner(),
        };

        if (dialog.ShowDialog() != true || dialog.DatabaseConfig == null)
            return;

        var key = AddDatabaseConfiguration(dialog.DatabaseConfig);
        _logger.LogDebug("Added database: {Name} with key {Key}", dialog.DatabaseConfig.Name, key);
    }

    /// <summary>
    /// Imports an original ClipMate 7.x database into a new database or an existing one.
    /// </summary>
    [RelayCommand]
    private void ImportClipMate7()
    {
        var targets = Databases
            .Where(p => !p.Configuration.ReadOnly && !string.IsNullOrWhiteSpace(p.FilePath) && File.Exists(Environment.ExpandEnvironmentVariables(p.FilePath)))
            .Select(p => new ClipMate7ImportTarget(p.Name, Environment.ExpandEnvironmentVariables(p.FilePath)));

        var viewModel = new ClipMate7ImportViewModel(_clipMate7ImportService, targets);
        var dialog = new ClipMate7ImportDialog(viewModel)
        {
            Owner = Application.Current.GetDialogOwner(),
        };

        dialog.ShowDialog();

        if (viewModel.CreatedDatabase is { } created)
        {
            var key = AddDatabaseConfiguration(created);
            SelectedDatabase = Databases.Last();
            _logger.LogInformation("Added imported ClipMate 7 database: {Name} with key {Key}", created.Name, key);
        }
        else if (viewModel.MergedDatabasePath is not null)
        {
            // Clips and collections were added to a database that may be open
            _messenger.Send(new RefreshCollectionTreeRequestedEvent());
            _messenger.Send(new ReloadClipsRequestedEvent());
        }
    }

    /// <summary>
    /// The sharing check for the Database with <paramref name="databaseKey" /> (null for a new one), against the other
    /// Databases as currently edited.
    /// </summary>
    private DatabaseSharingContext CreateSharingContext(string? databaseKey) =>
        new(_sharedDatabaseService, _databaseIdService,
            Databases.Where(p => p.DatabaseKey != databaseKey).Select(p => p.Configuration).ToList());

    private string AddDatabaseConfiguration(DatabaseConfiguration configuration)
    {
        // Generate a unique GUID-based key for the new database
        var guid = Guid.NewGuid().ToString("N");
        var key = $"db_{guid[..8]}";
        Databases.Add(new DatabaseConfigurationViewModel(key, configuration));
        return key;
    }

    /// <summary>
    /// Edits the selected database configuration.
    /// </summary>
    [RelayCommand]
    private async Task EditDatabaseAsync()
    {
        if (SelectedDatabase == null)
        {
            _logger.LogWarning("No database selected for editing");
            return;
        }

        var databaseId = await _databaseIdService.GetDatabaseIdAsync(SelectedDatabase.DatabaseKey);

        var dialog = new DatabaseEditDialog(SelectedDatabase.Configuration, databaseId, CreateSharingContext(SelectedDatabase.DatabaseKey))
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
