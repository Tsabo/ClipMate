using System.Collections.ObjectModel;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the QuickPaste toolbar.
/// </summary>
public partial class QuickPasteToolbarViewModel : ObservableObject
{
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<QuickPasteToolbarViewModel> _logger;
    private readonly IQuickPasteService _quickPasteService;

    [ObservableProperty]
    private string _currentTargetDisplay = "No target";

    [ObservableProperty]
    private string _currentTargetTooltip = "No active window detected";

    [ObservableProperty]
    private ObservableCollection<QuickPasteFormattingString> _formattingStrings = new();

    [ObservableProperty]
    private bool _goBackEnabled = true;

    [ObservableProperty]
    private QuickPasteFormattingString? _selectedFormattingString;

    [ObservableProperty]
    private string _targetLockIcon = "🔓";

    public QuickPasteToolbarViewModel(IQuickPasteService quickPasteService,
        IConfigurationService configurationService,
        ILogger<QuickPasteToolbarViewModel> logger)
    {
        _quickPasteService = quickPasteService ?? throw new ArgumentNullException(nameof(quickPasteService));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        LoadFormattingStrings();
        UpdateTargetDisplay();
        UpdateGoBackState();
    }

    /// <summary>
    /// Loads formatting strings from configuration.
    /// </summary>
    private void LoadFormattingStrings()
    {
        var config = _configurationService.Configuration.Preferences;
        FormattingStrings = new ObservableCollection<QuickPasteFormattingString>(config.QuickPasteFormattingStrings);
        SelectedFormattingString = _quickPasteService.GetSelectedFormattingString();
    }

    /// <summary>
    /// Updates the target display from the QuickPaste service.
    /// </summary>
    public void UpdateTargetDisplay()
    {
        var target = _quickPasteService.GetCurrentTarget();
        if (target.HasValue)
        {
            CurrentTargetDisplay = $"{target.Value.ProcessName}";
            CurrentTargetTooltip = $"Process: {target.Value.ProcessName}\nClass: {target.Value.ClassName}\nTitle: {target.Value.WindowTitle}";
        }
        else
        {
            CurrentTargetDisplay = "No target";
            CurrentTargetTooltip = "No active window detected";
        }

        TargetLockIcon = _quickPasteService.IsTargetLocked()
            ? "🔒"
            : "🔓";
    }

    /// <summary>
    /// Updates the GoBack enabled state from the QuickPaste service.
    /// </summary>
    public void UpdateGoBackState() => GoBackEnabled = _quickPasteService.GetGoBackState();

    /// <summary>
    /// Toggles the target lock state.
    /// </summary>
    [RelayCommand]
    private void ToggleTargetLock()
    {
        var isLocked = _quickPasteService.IsTargetLocked();
        _quickPasteService.SetTargetLock(!isLocked);
        TargetLockIcon = !isLocked
            ? "🔒"
            : "🔓";

        _logger.LogDebug("Target lock toggled to {State}", !isLocked);
    }

    /// <summary>
    /// Sends a TAB keystroke to the target application.
    /// </summary>
    [RelayCommand]
    private void SendTab()
    {
        try
        {
            _quickPasteService.SendTabKeystroke();
            _logger.LogDebug("TAB keystroke sent");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send TAB keystroke");
        }
    }

    /// <summary>
    /// Sends an ENTER keystroke to the target application.
    /// </summary>
    [RelayCommand]
    private void SendEnter()
    {
        try
        {
            _quickPasteService.SendEnterKeystroke();
            _logger.LogDebug("ENTER keystroke sent");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send ENTER keystroke");
        }
    }

    /// <summary>
    /// Toggles the GoBack state.
    /// </summary>
    [RelayCommand]
    private void ToggleGoBack()
    {
        var current = _quickPasteService.GetGoBackState();
        _quickPasteService.SetGoBackState(!current);
        GoBackEnabled = !current;
        _logger.LogDebug("GoBack toggled to {State}", !current);
    }

    partial void OnSelectedFormattingStringChanged(QuickPasteFormattingString? value)
    {
        if (value != null)
        {
            _quickPasteService.SelectFormattingString(value);
            _logger.LogDebug("Selected formatting string: {Title}", value.Title);
        }
    }
}
