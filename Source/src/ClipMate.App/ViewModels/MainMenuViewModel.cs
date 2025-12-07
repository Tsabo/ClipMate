using ClipMate.Core.Events;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace ClipMate.App.ViewModels;

/// <summary>
/// Shared ViewModel for the main menu across both Explorer and Classic windows.
/// Contains all menu commands that are common to both window types.
/// </summary>
public partial class MainMenuViewModel : ObservableObject
{
    private readonly IMessenger _messenger;

    public MainMenuViewModel(IMessenger messenger)
    {
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
    }

    [ObservableProperty]
    private bool _isExplodeMode;

    [ObservableProperty]
    private bool _isLoopMode;

    // ==========================
    // File Menu Commands
    // ==========================

    [RelayCommand]
    private void CreateNewClip() { }

    [RelayCommand]
    private void ClipProperties() { }

    [RelayCommand]
    private void RenameClip() { }

    [RelayCommand]
    private void DeleteSelected() { }

    [RelayCommand]
    private void UnDelete() { }

    [RelayCommand]
    private void CopyToCollection() { }

    [RelayCommand]
    private void MoveToCollection() { }

    [RelayCommand]
    private void ExportClips() { }

    [RelayCommand]
    private void ExportToXml() { }

    [RelayCommand]
    private void ImportFromXml() { }

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

    [RelayCommand]
    private void Print() { }

    [RelayCommand]
    private void EnableQuickPrint() { }

    [RelayCommand]
    private void PrintOptions() { }

    [RelayCommand]
    private void Exit()
    {
        _messenger.Send(new ExitApplicationEvent());
    }

    // ==========================
    // Edit Menu Commands
    // ==========================

    [RelayCommand]
    private void Undo() { }

    [RelayCommand]
    private void SelectAll() { }

    [RelayCommand]
    private void CaptureSpecial()
    {
        _messenger.Send(new ManualCaptureClipboardEvent());
    }

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
    private void ChangeTitle() { }

    [RelayCommand]
    private void StripNonText() { }

    [RelayCommand]
    private void ConvertFilePointer() { }

    [RelayCommand]
    private void UnicodeToAnsi() { }

    [RelayCommand]
    private void PowerPasteUp() { }

    [RelayCommand]
    private void PowerPasteDown() { }

    [RelayCommand]
    private void PowerPasteToggle() { }

    // ==========================
    // Tools Menu Commands
    // ==========================

    [RelayCommand]
    private void AutoCapture()
    {
        _messenger.Send(new ToggleAutoCaptureEvent());
    }

    [RelayCommand]
    private void FilterOutbound() { }

    [RelayCommand]
    private void Options()
    {
        _messenger.Send(new OpenOptionsDialogEvent());
    }

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
    private void ManageTemplates()
    {
        _messenger.Send(new OpenTemplateEditorDialogEvent());
    }

    [RelayCommand]
    private void TextTools()
    {
        _messenger.Send(new OpenTextToolsDialogEvent());
    }

    // ==========================
    // View Menu Commands
    // ==========================

    [RelayCommand]
    private void Search() { }

    [RelayCommand]
    private void SelectPrevious()
    {
        _messenger.Send(new SelectPreviousClipEvent());
    }

    [RelayCommand]
    private void SelectNext()
    {
        _messenger.Send(new SelectNextClipEvent());
    }

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
    private void SwitchView()
    {
        _messenger.Send(new ShowExplorerWindowEvent());
    }

    [RelayCommand]
    private void OpenExplorer()
    {
        _messenger.Send(new ShowExplorerWindowEvent());
    }

    [RelayCommand]
    private void OpenClassic()
    {
        _messenger.Send(new ShowClipBarRequestedEvent());
    }

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
