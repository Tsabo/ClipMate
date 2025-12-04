using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the ClipViewer toolbar providing text editing and transformation operations.
/// </summary>
public partial class ClipViewerToolbarViewModel : ObservableObject
{
    private readonly ILogger<ClipViewerToolbarViewModel> _logger;

    [ObservableProperty]
    private bool _isTacked;

    [ObservableProperty]
    private bool _isWordWrapEnabled;

    [ObservableProperty]
    private bool _showNonPrintingCharacters;

    public ClipViewerToolbarViewModel(ILogger<ClipViewerToolbarViewModel> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region Help Command

    [RelayCommand]
    private void ShowHelp()
    {
        _logger.LogDebug("Help requested");
        OnShowHelpRequested?.Invoke();
    }

    #endregion

    #region Action Handlers (set by ClipViewerControl)

    public Action? OnNewClipRequested { get; set; }

    public Action? OnCutRequested { get; set; }

    public Action? OnCopyRequested { get; set; }

    public Action? OnPasteRequested { get; set; }

    public Action<string>? OnRemoveLineBreaksRequested { get; set; }

    public Action<string>? OnConvertCaseRequested { get; set; }

    public Action? OnTrimRequested { get; set; }

    public Action? OnOpenTextCleanupDialogRequested { get; set; }

    public Action? OnUndoRequested { get; set; }

    public Action? OnFindRequested { get; set; }

    public Action? OnShowHelpRequested { get; set; }

    #endregion

    #region View Commands

    [RelayCommand]
    private void ToggleTack()
    {
        IsTacked = !IsTacked;
        _logger.LogDebug("Tack toggled: {IsTacked}", IsTacked);
    }

    [RelayCommand]
    private void ToggleWordWrap()
    {
        IsWordWrapEnabled = !IsWordWrapEnabled;
        _logger.LogDebug("Word wrap toggled: {IsWordWrapEnabled}", IsWordWrapEnabled);
    }

    [RelayCommand]
    private void ToggleShowNonPrinting()
    {
        ShowNonPrintingCharacters = !ShowNonPrintingCharacters;
        _logger.LogDebug("Show non-printing characters toggled: {ShowNonPrintingCharacters}", ShowNonPrintingCharacters);
    }

    #endregion

    #region Clipboard Commands

    [RelayCommand]
    private void NewClip()
    {
        _logger.LogDebug("New clip requested");
        OnNewClipRequested?.Invoke();
    }

    [RelayCommand]
    private void Cut()
    {
        _logger.LogDebug("Cut requested");
        OnCutRequested?.Invoke();
    }

    [RelayCommand]
    private void Copy()
    {
        _logger.LogDebug("Copy requested");
        OnCopyRequested?.Invoke();
    }

    [RelayCommand]
    private void Paste()
    {
        _logger.LogDebug("Paste requested");
        OnPasteRequested?.Invoke();
    }

    #endregion

    #region Transform Commands

    [RelayCommand]
    private void RemoveLineBreaks(string mode)
    {
        _logger.LogDebug("Remove line breaks requested with mode: {Mode}", mode);
        OnRemoveLineBreaksRequested?.Invoke(mode);
    }

    [RelayCommand]
    private void ConvertCase(string caseType)
    {
        _logger.LogDebug("Convert case requested: {CaseType}", caseType);
        OnConvertCaseRequested?.Invoke(caseType);
    }

    [RelayCommand]
    private void Trim()
    {
        _logger.LogDebug("Trim requested");
        OnTrimRequested?.Invoke();
    }

    [RelayCommand]
    private void OpenTextCleanupDialog()
    {
        _logger.LogDebug("Text Cleanup dialog requested");
        OnOpenTextCleanupDialogRequested?.Invoke();
    }

    #endregion

    #region Edit Commands

    [RelayCommand]
    private void Undo()
    {
        _logger.LogDebug("Undo requested");
        OnUndoRequested?.Invoke();
    }

    [RelayCommand]
    private void Find()
    {
        _logger.LogDebug("Find requested");
        OnFindRequested?.Invoke();
    }

    #endregion
}
