using System.Collections.ObjectModel;
using System.Diagnostics;
using ClipMate.App.Services;
using ClipMate.App.Views.Dialogs;
using ClipMate.Core.Events;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Data.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using IMessenger = CommunityToolkit.Mvvm.Messaging.IMessenger;
using Application = System.Windows.Application;
using Window = System.Windows.Window;

namespace ClipMate.App.ViewModels;

/// <summary>
/// Shared ViewModel for the main menu across both Explorer and Classic windows.
/// Contains all menu commands that are common to both window types.
/// </summary>
public partial class MainMenuViewModel : ObservableObject
{
    private readonly IClipViewerWindowManager _clipViewerWindowManager;
    private readonly IMessenger _messenger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IUndoService _undoService;

    /// <summary>
    /// Gets or sets whether GoBack is enabled for QuickPaste.
    /// When enabled, focus returns to ClipMate after pasting.
    /// </summary>
    [ObservableProperty]
    private bool _goBackEnabled;

    /// <summary>
    /// Gets whether there are multiple databases loaded (determines whether to show submenu).
    /// </summary>
    [ObservableProperty]
    private bool _hasMultipleDatabases;

    [ObservableProperty]
    private bool _isExplodeMode;

    [ObservableProperty]
    private bool _isLoopMode;

    /// <summary>
    /// Gets or sets whether outbound clip filtering is enabled.
    /// When enabled, clipboard contents are replaced with plain-text version after capture.
    /// </summary>
    [ObservableProperty]
    private bool _isOutboundFilterEnabled;

    /// <summary>
    /// Gets or sets whether target is locked for QuickPaste.
    /// </summary>
    [ObservableProperty]
    private bool _isTargetLocked;

    public MainMenuViewModel(IMessenger messenger,
        IUndoService undoService,
        IClipViewerWindowManager clipViewerWindowManager,
        IServiceProvider serviceProvider)
    {
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _undoService = undoService ?? throw new ArgumentNullException(nameof(undoService));
        _clipViewerWindowManager = clipViewerWindowManager ?? throw new ArgumentNullException(nameof(clipViewerWindowManager));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// Gets the collection of loaded databases for dynamic menu generation.
    /// </summary>
    public ObservableCollection<DatabaseMenuItemViewModel> LoadedDatabases { get; } = [];

    /// <summary>
    /// Refreshes the loaded databases collection.
    /// Call this when databases are loaded/unloaded.
    /// </summary>
    public void RefreshLoadedDatabases()
    {
        var databaseManager = _serviceProvider.GetRequiredService<IDatabaseManager>();
        var databases = databaseManager.GetLoadedDatabases().ToList();

        LoadedDatabases.Clear();
        foreach (var item in databases)
            LoadedDatabases.Add(new DatabaseMenuItemViewModel(item.Name, GetDatabaseKey(item)));

        HasMultipleDatabases = databases.Count > 1;
    }

    /// <summary>
    /// Gets the database key from a DatabaseConfiguration.
    /// The key is the normalized file path (used for lookup).
    /// </summary>
    private static string GetDatabaseKey(DatabaseConfiguration config) => config.FilePath;

    // ==========================
    // File Menu Commands
    // ==========================

    [RelayCommand]
    private void CreateNewClip() => _messenger.Send(new CreateNewClipRequestedEvent(Guid.Empty));

    [RelayCommand]
    private void ClipProperties() => _messenger.Send(new ShowClipPropertiesRequestedEvent());

    [RelayCommand]
    private void RenameClip() => _messenger.Send(new RenameClipRequestedEvent(Guid.Empty, string.Empty));

    [RelayCommand]
    private void DeleteSelected() => _messenger.Send(new DeleteClipsRequestedEvent([]));

    [RelayCommand]
    private void UnDelete() => _messenger.Send(new RestoreClipsRequestedEvent());

    [RelayCommand]
    private void CopyToCollection() => _messenger.Send(new CopyToCollectionRequestedEvent([]));

    [RelayCommand]
    private void MoveToCollection() => _messenger.Send(new MoveToCollectionRequestedEvent([]));

    [RelayCommand]
    private void ExportClips() => _messenger.Send(new ExportToFilesRequestedEvent());

    [RelayCommand]
    private void ExportToXml() => _messenger.Send(new ExportToXmlRequestedEvent());

    [RelayCommand]
    private void ImportFromXml()
    {
        var vm = ActivatorUtilities.CreateInstance<XmlImportViewModel>(_serviceProvider);
        var dialog = new XmlImportDialog(vm)
        {
            Owner = Application.Current.GetDialogOwner(),
        };

        dialog.ShowDialog();
    }

    [RelayCommand]
    private void DecryptClips() { }

    [RelayCommand]
    private void EncryptClips() { }

    [RelayCommand]
    private void ForgetEncryptionKey() { }

    [RelayCommand]
    private void ShowProperties() => _messenger.Send(new ShowCollectionPropertiesRequestedEvent());

    [RelayCommand]
    private void AddCollection() => _messenger.Send(new AddCollectionRequestedEvent());

    [RelayCommand]
    private void DeleteCollection() => _messenger.Send(new DeleteCollectionRequestedEvent());

    [RelayCommand]
    private void ReloadCollection() => _messenger.Send(new ReloadCollectionRequestedEvent());

    [RelayCommand]
    private void ResequenceSortKeys() => _messenger.Send(new ResequenceSortKeysRequestedEvent());

    [RelayCommand]
    private void ActivateDatabase()
    {
        // Get the configuration service and database manager to find unloaded databases
        var configService = _serviceProvider.GetRequiredService<IConfigurationService>();
        var databaseManager = _serviceProvider.GetRequiredService<IDatabaseManager>();
        var loadedKeys = databaseManager.GetLoadedDatabases().Select(db => db.FilePath).ToHashSet();

        // Find databases that are configured but not loaded
        var inactiveDatabases = configService.Configuration.Databases.Values
            .Where(db => !loadedKeys.Contains(db.FilePath))
            .ToList();

        if (inactiveDatabases.Count == 0)
        {
            _messenger.Send(new StatusUpdateEvent("All databases are already active"));
            return;
        }

        // For now, activate the first inactive database
        // TODO: Could show a picker dialog if multiple inactive databases exist
        var database = inactiveDatabases.First();
        _messenger.Send(new ActivateDatabaseRequestedEvent(database.FilePath));
    }

    [RelayCommand]
    private void DeactivateDatabase()
    {
        // Get the database manager and show active databases
        var databaseManager = _serviceProvider.GetRequiredService<IDatabaseManager>();
        var databases = databaseManager.GetLoadedDatabases().ToList();

        if (databases.Count <= 1)
        {
            _messenger.Send(new StatusUpdateEvent("Cannot deactivate the only active database"));
            return;
        }

        // For now, deactivate the last loaded database (not the first/primary one)
        // TODO: Could show a picker dialog if multiple databases are loaded
        var database = databases.Last();
        _messenger.Send(new DeactivateDatabaseRequestedEvent(database.FilePath));
    }

    // Database Maintenance submenu
    [RelayCommand]
    private void BackupDatabase() => _messenger.Send(new BackupDatabaseRequestedEvent());

    [RelayCommand]
    private void RestoreDatabase() => _messenger.Send(new RestoreDatabaseRequestedEvent());

    [RelayCommand]
    private void EmptyTrash() => _messenger.Send(new EmptyTrashRequestedEvent());

    [RelayCommand]
    private void SimpleRepair() => _messenger.Send(new SimpleRepairRequestedEvent());

    [RelayCommand]
    private void ComprehensiveRepair() => _messenger.Send(new ComprehensiveRepairRequestedEvent());

    [RelayCommand]
    private void RunCleanupNow() => _messenger.Send(new RunCleanupNowRequestedEvent());

    [RelayCommand]
    private void Print() { }

    [RelayCommand]
    private void EnableQuickPrint() { }

    [RelayCommand]
    private void PrintOptions() { }

    [RelayCommand]
    private void Exit() => _messenger.Send(new ExitApplicationEvent());

    // ==========================
    // Edit Menu Commands
    // ==========================

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        var previousState = _undoService.Undo();
        if (previousState != null)
            _messenger.Send(new RestoreTextStateEvent(previousState));
    }

    private bool CanUndo() => _undoService.CanUndo;

    [RelayCommand]
    private void SelectAll() => _messenger.Send(new SelectAllClipsRequestedEvent());

    [RelayCommand]
    private void CaptureSpecial() => _messenger.Send(new ManualCaptureClipboardEvent());

    [RelayCommand]
    private void AppendClips() => _messenger.Send(new AppendClipsRequestedEvent());

    [RelayCommand]
    private void CleanUpText() => _messenger.Send(new CleanUpTextRequestedEvent());

    [RelayCommand]
    private void ShiftLeft() { } // Deferred to Step 6

    [RelayCommand]
    private void ShiftRight() { } // Deferred to Step 6

    [RelayCommand]
    private void RemoveLineBreaks() => _messenger.Send(new RemoveLineBreaksRequestedEvent());

    [RelayCommand]
    private void CheckSpelling() { } // Deferred to Step 6

    [RelayCommand]
    private void ChangeTitle() => _messenger.Send(new RenameClipRequestedEvent(Guid.Empty, string.Empty));

    [RelayCommand]
    private void StripNonText() => _messenger.Send(new StripNonTextRequestedEvent());

    // Case conversion commands
    [RelayCommand]
    private void ToUpperCase() => _messenger.Send(new CaseConversionRequestedEvent(CaseConversionType.Upper));

    [RelayCommand]
    private void ToLowerCase() => _messenger.Send(new CaseConversionRequestedEvent(CaseConversionType.Lower));

    [RelayCommand]
    private void ToTitleCase() => _messenger.Send(new CaseConversionRequestedEvent(CaseConversionType.Title));

    [RelayCommand]
    private void ToSentenceCase() => _messenger.Send(new CaseConversionRequestedEvent(CaseConversionType.Sentence));

    [RelayCommand]
    private void ToggleCase() => _messenger.Send(new CaseConversionRequestedEvent(CaseConversionType.Toggle));

    [RelayCommand]
    private void ConvertFilePointer() { }

    [RelayCommand]
    private void UnicodeToAnsi() { }

    [RelayCommand]
    private void PowerPasteUp() => _messenger.Send(new PowerPasteUpRequestedEvent());

    [RelayCommand]
    private void PowerPasteDown() => _messenger.Send(new PowerPasteDownRequestedEvent());

    [RelayCommand]
    private void PowerPasteToggle() => _messenger.Send(new PowerPasteToggleRequestedEvent());

    // ==========================
    // Tools Menu Commands
    // ==========================

    [RelayCommand]
    private void AutoCapture() => _messenger.Send(new ToggleAutoCaptureEvent());

    partial void OnIsOutboundFilterEnabledChanged(bool value) => _messenger.Send(new OutboundFilterToggleEvent(value));

    [RelayCommand]
    private void Options() => _messenger.Send(new OpenOptionsDialogEvent());

    [RelayCommand]
    private void AppProfile() => _messenger.Send(new OpenOptionsDialogEvent("AppProfiles"));

    [RelayCommand]
    private void Language() => _messenger.Send(new OpenOptionsDialogEvent("FontLanguage"));

    [RelayCommand]
    private void ClipboardDiagnostics()
    {
        var viewModel = _serviceProvider.GetRequiredService<ClipboardDiagnosticsViewModel>();
        var activeWindowService = _serviceProvider.GetRequiredService<IActiveWindowService>();
        var dialog = new ClipboardDiagnosticsDialog(viewModel)
        {
            Owner = activeWindowService.DialogOwner,
        };

        dialog.ShowDialog();
    }

    [RelayCommand]
    private void TracePaste()
    {
        var viewModel = _serviceProvider.GetRequiredService<PasteTraceViewModel>();
        var activeWindowService = _serviceProvider.GetRequiredService<IActiveWindowService>();
        var dialog = new PasteTraceDialog(viewModel)
        {
            Owner = activeWindowService.DialogOwner,
        };

        dialog.Show(); // Modeless
    }

    [RelayCommand]
    private void ReestablishClipboard() { }

    /// <summary>
    /// Shows the SQL Maintenance window for the active database.
    /// Used when there's only one database loaded.
    /// </summary>
    [RelayCommand]
    private void ShowSqlWindow()
    {
        var collectionService = _serviceProvider.GetRequiredService<ICollectionService>();
        var databaseKey = collectionService.GetActiveDatabaseKey();
        if (string.IsNullOrEmpty(databaseKey))
            return;

        ShowSqlWindowForDatabase(databaseKey);
    }

    /// <summary>
    /// Shows the SQL Maintenance window for a specific database.
    /// Used from the dynamic submenu when multiple databases are loaded.
    /// </summary>
    /// <param name="databaseKey">The database key (file path) to open.</param>
    [RelayCommand]
    private void ShowSqlWindowForDatabase(string? databaseKey)
    {
        if (string.IsNullOrEmpty(databaseKey))
            return;

        var viewModel = _serviceProvider.GetRequiredService<SqlMaintenanceViewModel>();
        var configService = _serviceProvider.GetRequiredService<IConfigurationService>();
        var sqlMaintenanceFactory = _serviceProvider.GetRequiredService<ISqlMaintenanceServiceFactory>();
        var logger = _serviceProvider.GetRequiredService<ILogger<SqlMaintenanceDialog>>();
        var activeWindowService = _serviceProvider.GetRequiredService<IActiveWindowService>();

        var sqlMaintenanceService = sqlMaintenanceFactory.Create(databaseKey);
        var dialog = new SqlMaintenanceDialog(viewModel, sqlMaintenanceService, configService, logger)
        {
            Owner = activeWindowService.DialogOwner,
        };

        dialog.ShowDialog();
    }

    [RelayCommand]
    private void ShowEventLog()
    {
        var viewModel = _serviceProvider.GetRequiredService<EventLogViewModel>();
        var activeWindowService = _serviceProvider.GetRequiredService<IActiveWindowService>();
        var dialog = new EventLogDialog(viewModel)
        {
            Owner = activeWindowService.DialogOwner,
        };

        dialog.ShowDialog();
    }

    [RelayCommand]
    private void ManageTemplates() => _messenger.Send(new OpenTemplateEditorDialogEvent());

    [RelayCommand]
    private void TextTools() => _messenger.Send(new OpenTextToolsDialogEvent());

    // ==========================
    // View Menu Commands
    // ==========================

    [RelayCommand]
    private void Search() => _messenger.Send(new ShowSearchWindowEvent());

    [RelayCommand]
    private void SelectPrevious() => _messenger.Send(new SelectPreviousClipEvent());

    [RelayCommand]
    private void SelectNext() => _messenger.Send(new SelectNextClipEvent());

    [RelayCommand]
    private void SwitchToLastCollection() => _messenger.Send(new SwitchToLastCollectionRequestedEvent());

    [RelayCommand]
    private void SwitchToFavoriteCollection() => _messenger.Send(new SwitchToFavoriteCollectionRequestedEvent());

    [RelayCommand]
    private void SelectCollection() { }

    [RelayCommand]
    private void OpenSourceUrl() => _messenger.Send(new OpenSourceUrlRequestedEvent());

    [RelayCommand]
    private void LaunchCharMap()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "charmap.exe",
                UseShellExecute = true,
            });
        }
        catch
        {
            // Silently fail if charmap is not available
        }
    }

    [RelayCommand]
    private void ViewClip() => _clipViewerWindowManager.ToggleVisibility();

    [RelayCommand]
    private void SwitchView() => _messenger.Send(new ShowExplorerWindowEvent());

    [RelayCommand]
    private void OpenExplorer() => _messenger.Send(new ShowExplorerWindowEvent());

    [RelayCommand]
    private void OpenClassic() => _messenger.Send(new ShowClipBarRequestedEvent());

    [RelayCommand]
    private void CloseAllWindows()
    {
        // Close all windows except the main hidden window that hosts the tray icon
        var windowsToClose = Application.Current.Windows
            .Cast<Window>()
            .Where(w => w.IsVisible && w != Application.Current.MainWindow)
            .ToList();

        foreach (var window in windowsToClose)
            window.Close();
    }

    [RelayCommand]
    private void Transparency() { }

    // ==========================
    // Help Menu Commands
    // ==========================

    [RelayCommand]
    private void About() { }

    [RelayCommand]
    private void Documentation()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/Jelosus2/ClipMate/wiki",
            UseShellExecute = true,
        });
    }

    [RelayCommand]
    private void ViewOnGitHub()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/Jelosus2/ClipMate",
            UseShellExecute = true,
        });
    }

    // ==========================
    // Toolbar Commands
    // ==========================

    [RelayCommand]
    private void MoveToInbox() => _messenger.Send(new MoveToNamedCollectionRequestedEvent("Inbox"));

    [RelayCommand]
    private void MoveToSafe() => _messenger.Send(new MoveToNamedCollectionRequestedEvent("Safe"));

    [RelayCommand]
    private void MoveToOverflow() => _messenger.Send(new MoveToNamedCollectionRequestedEvent("Overflow"));

    [RelayCommand]
    private void MoveToSamples() => _messenger.Send(new MoveToNamedCollectionRequestedEvent("Samples"));

    [RelayCommand]
    private void MoveToTrash() => _messenger.Send(new MoveToNamedCollectionRequestedEvent("Trash"));

    [RelayCommand]
    private void SelectInbox() => _messenger.Send(new SelectNamedCollectionRequestedEvent("Inbox"));

    [RelayCommand]
    private void SelectSafe() => _messenger.Send(new SelectNamedCollectionRequestedEvent("Safe"));

    [RelayCommand]
    private void SelectOverflow() => _messenger.Send(new SelectNamedCollectionRequestedEvent("Overflow"));

    [RelayCommand]
    private void SelectSamples() => _messenger.Send(new SelectNamedCollectionRequestedEvent("Samples"));

    [RelayCommand]
    private void SelectTrash() => _messenger.Send(new SelectNamedCollectionRequestedEvent("Trash"));

    // ==========================
    // QuickPaste Menu Commands
    // ==========================

    [RelayCommand]
    private void SendTab() => _messenger.Send(new QuickPasteSendTabEvent());

    [RelayCommand]
    private void SendEnter() => _messenger.Send(new QuickPasteSendEnterEvent());

    [RelayCommand]
    private void QuickPasteSettings() => _messenger.Send(new OpenOptionsDialogEvent("QuickPaste"));
}
