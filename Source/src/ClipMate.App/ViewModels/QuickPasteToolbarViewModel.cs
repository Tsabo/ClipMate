using System.Collections.ObjectModel;
using ClipMate.Core.Events;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the QuickPaste toolbar.
/// </summary>
public partial class QuickPasteToolbarViewModel : ObservableObject, IRecipient<ShortcutModeStatusMessage>
{
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<QuickPasteToolbarViewModel> _logger;
    private readonly IMessenger _messenger;
    private readonly IQuickPasteService _quickPasteService;

    [ObservableProperty]
    private string _currentTargetDisplay = "No target";

    [ObservableProperty]
    private string _currentTargetString = string.Empty;

    [ObservableProperty]
    private string _currentTargetTooltip = "No active window detected";

    [ObservableProperty]
    private ObservableCollection<QuickPasteFormattingString> _formattingStrings = [];

    [ObservableProperty]
    private bool _goBackEnabled;

    [ObservableProperty]
    private bool _hasTarget;

    [ObservableProperty]
    private bool _isShortcutModeActive;

    [ObservableProperty]
    private bool _isTargetLocked;

    [ObservableProperty]
    private QuickPasteFormattingString? _selectedFormattingString;

    [ObservableProperty]
    private string _shortcutFilterText = string.Empty;

    [ObservableProperty]
    private string _targetLockIcon = "🔓";

    public QuickPasteToolbarViewModel(IQuickPasteService quickPasteService,
        IConfigurationService configurationService,
        IMessenger messenger,
        ILogger<QuickPasteToolbarViewModel> logger)
    {
        _quickPasteService = quickPasteService ?? throw new ArgumentNullException(nameof(quickPasteService));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Subscribe to target change events
        _messenger.Register<QuickPasteTargetChangedEvent>(this, (_, _) => UpdateTargetDisplay());
        _messenger.Register(this);

        LoadFormattingStrings();
        UpdateTargetDisplay();
        UpdateGoBackState();
    }

    /// <summary>
    /// Receives ShortcutModeStatusMessage to display shortcut filter on toolbar.
    /// </summary>
    public void Receive(ShortcutModeStatusMessage message)
    {
        IsShortcutModeActive = message.IsActive;
        ShortcutFilterText = message.IsActive
            ? message.Filter
            : string.Empty;
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
            HasTarget = true;
            CurrentTargetString = $"{target.Value.ProcessName}:{target.Value.ClassName}";

            // Display process name with window title
            var displayText = !string.IsNullOrWhiteSpace(target.Value.WindowTitle)
                ? $"{target.Value.ProcessName} - {target.Value.WindowTitle}"
                : target.Value.ProcessName;

            CurrentTargetDisplay = displayText;
            CurrentTargetTooltip = $"Process: {target.Value.ProcessName}\nClass: {target.Value.ClassName}\nTitle: {target.Value.WindowTitle}";
        }
        else
        {
            HasTarget = false;
            CurrentTargetString = string.Empty;
            CurrentTargetDisplay = "No target";
            CurrentTargetTooltip = "No active window detected";
        }

        IsTargetLocked = _quickPasteService.IsTargetLocked();
        TargetLockIcon = IsTargetLocked
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
        IsTargetLocked = !isLocked;
        TargetLockIcon = !isLocked
            ? "🔒"
            : "🔓";

        _logger.LogDebug("Target lock toggled to {State}", !isLocked);
    }

    /// <summary>
    /// Pastes the currently selected clip immediately (triggered by clicking target text).
    /// </summary>
    [RelayCommand]
    private void PasteNow()
    {
        _logger.LogDebug("PasteNow triggered from toolbar, sending QuickPasteNowEvent");
        _messenger.Send(new QuickPasteNowEvent());
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

    /// <summary>
    /// Adds the current target to the good target list.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasTarget))]
    private async Task AddToGoodTargets()
    {
        var config = _configurationService.Configuration.Preferences;
        if (!string.IsNullOrEmpty(CurrentTargetString) && !config.QuickPasteGoodTargets.Contains(CurrentTargetString))
        {
            config.QuickPasteGoodTargets.Add(CurrentTargetString);
            await _configurationService.SaveAsync();
            _logger.LogInformation("Added {Target} to good targets", CurrentTargetString);
        }
    }

    /// <summary>
    /// Adds the current target to the bad target list.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasTarget))]
    private async Task AddToBadTargets()
    {
        var config = _configurationService.Configuration.Preferences;
        if (!string.IsNullOrEmpty(CurrentTargetString) && !config.QuickPasteBadTargets.Contains(CurrentTargetString))
        {
            config.QuickPasteBadTargets.Add(CurrentTargetString);
            await _configurationService.SaveAsync();
            _logger.LogInformation("Added {Target} to bad targets", CurrentTargetString);
        }
    }

    /// <summary>
    /// Opens the Options dialog to the QuickPaste settings tab.
    /// </summary>
    [RelayCommand]
    private void OpenQuickPasteSettings()
    {
        _logger.LogDebug("Opening QuickPaste settings");
        // Send message to open options dialog with QuickPaste tab selected
        _messenger.Send(new OpenOptionsDialogEvent("QuickPaste"));
    }
}
