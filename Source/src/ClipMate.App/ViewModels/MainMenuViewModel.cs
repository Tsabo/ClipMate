using ClipMate.App.Views.Dialogs;
using ClipMate.Core.Events;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using IMessenger = CommunityToolkit.Mvvm.Messaging.IMessenger;
using Application = System.Windows.Application;

namespace ClipMate.App.ViewModels;

/// <summary>
/// Shared ViewModel for the main menu across both Explorer and Classic windows.
/// Contains all menu commands that are common to both window types.
/// </summary>
public partial class MainMenuViewModel : ObservableObject
{
    private readonly IMessenger _messenger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IUndoService _undoService;

    [ObservableProperty]
    private bool _isExplodeMode;

    [ObservableProperty]
    private bool _isLoopMode;

    public MainMenuViewModel(IMessenger messenger,
        IUndoService undoService,
        IServiceProvider serviceProvider)
    {
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _undoService = undoService ?? throw new ArgumentNullException(nameof(undoService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    // ==========================
    // File Menu Commands
    // ==========================

    [RelayCommand]
    private void CreateNewClip() => _messenger.Send(new CreateNewClipRequestedEvent(Guid.Empty));

    [RelayCommand]
    private void ClipProperties() { }

    [RelayCommand]
    private void RenameClip() => _messenger.Send(new RenameClipRequestedEvent(Guid.Empty, string.Empty));

    [RelayCommand]
    private void DeleteSelected() => _messenger.Send(new DeleteClipsRequestedEvent([]));

    [RelayCommand]
    private void UnDelete() { }

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
    private void ShowProperties() { }

    [RelayCommand]
    private void AddCollection() { }

    [RelayCommand]
    private void DeleteCollection() { }

    [RelayCommand]
    private void ReloadCollection() { }

    [RelayCommand]
    private void ResequenceSortKeys() { }

    [RelayCommand]
    private void ActivateDatabase() { }

    [RelayCommand]
    private void DeactivateDatabase() { }

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
    private void SelectAll() { }

    [RelayCommand]
    private void CaptureSpecial() => _messenger.Send(new ManualCaptureClipboardEvent());

    [RelayCommand]
    private void AppendClips() { }

    [RelayCommand]
    private void CleanUpText() { }

    [RelayCommand]
    private void ShiftLeft() { }

    [RelayCommand]
    private void ShiftRight() { }

    [RelayCommand]
    private void RemoveLineBreaks() { }

    [RelayCommand]
    private void CheckSpelling() { }

    [RelayCommand]
    private void ChangeTitle() => _messenger.Send(new RenameClipRequestedEvent(Guid.Empty, string.Empty));

    [RelayCommand]
    private void StripNonText() { }

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

    [RelayCommand]
    private void FilterOutbound() { }

    [RelayCommand]
    private void Options() => _messenger.Send(new OpenOptionsDialogEvent());

    [RelayCommand]
    private void AppProfile() { }

    [RelayCommand]
    private void Language() { }

    [RelayCommand]
    private void ConnectToClipBar() { }

    [RelayCommand]
    private void ClipboardDiagnostics() { }

    [RelayCommand]
    private void TracePaste() { }

    [RelayCommand]
    private void ReestablishClipboard() { }

    [RelayCommand]
    private void ShowSqlWindow() { }

    [RelayCommand]
    private void ShowEventLog() { }

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
    private void SwitchToLastCollection() { }

    [RelayCommand]
    private void SwitchToFavoriteCollection() { }

    [RelayCommand]
    private void SelectCollection() { }

    [RelayCommand]
    private void OpenSourceUrl() { }

    [RelayCommand]
    private void LaunchCharMap() { }

    [RelayCommand]
    private void ViewClip() { }

    [RelayCommand]
    private void SwitchView() => _messenger.Send(new ShowExplorerWindowEvent());

    [RelayCommand]
    private void OpenExplorer() => _messenger.Send(new ShowExplorerWindowEvent());

    [RelayCommand]
    private void OpenClassic() => _messenger.Send(new ShowClipBarRequestedEvent());

    [RelayCommand]
    private void CloseAllWindows() { }

    [RelayCommand]
    private void Transparency() { }

    // ==========================
    // Help Menu Commands
    // ==========================

    [RelayCommand]
    private void About() { }

    [RelayCommand]
    private void Documentation() { }

    [RelayCommand]
    private void ViewOnGitHub() { }

    // ==========================
    // Toolbar Commands
    // ==========================

    [RelayCommand]
    private void MoveToInbox() { }

    [RelayCommand]
    private void MoveToSafe() { }

    [RelayCommand]
    private void MoveToOverflow() { }

    [RelayCommand]
    private void MoveToSamples() { }

    [RelayCommand]
    private void MoveToTrash() { }

    [RelayCommand]
    private void SelectInbox() { }

    [RelayCommand]
    private void SelectSafe() { }

    [RelayCommand]
    private void SelectOverflow() { }

    [RelayCommand]
    private void SelectSamples() { }

    [RelayCommand]
    private void SelectTrash() { }
}
