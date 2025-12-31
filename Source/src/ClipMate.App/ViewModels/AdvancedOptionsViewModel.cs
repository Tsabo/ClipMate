using ClipMate.Core.Models;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using DialogResult = ClipMate.Core.Models.DialogResult;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the Advanced options tab.
/// </summary>
public partial class AdvancedOptionsViewModel : ObservableObject
{
    private readonly IConfigurationService _configurationService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<AdvancedOptionsViewModel> _logger;

    [ObservableProperty]
    private bool _altKeyRequiredForDragDrop;

    [ObservableProperty]
    private int _captureDelayMs;

    [ObservableProperty]
    private bool _enableCachedDatabaseWrites;

    [ObservableProperty]
    private bool _payAttentionToClipboardIgnoreFlag;

    [ObservableProperty]
    private int _powerPasteDelay;

    [ObservableProperty]
    private bool _reuseLastSelectedMoveTarget;

    [ObservableProperty]
    private int _settleTimeBetweenCapturesMs;

    [ObservableProperty]
    private int _startupDelaySeconds;

    [ObservableProperty]
    private bool _useDdeForBrowserUrl;

    public AdvancedOptionsViewModel(IConfigurationService configurationService,
        IDialogService dialogService,
        ILogger<AdvancedOptionsViewModel> logger)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Loads advanced configuration.
    /// </summary>
    public void LoadAsync()
    {
        var config = _configurationService.Configuration.Preferences;

        StartupDelaySeconds = config.StartupDelaySeconds;
        CaptureDelayMs = config.CaptureDelayMs;
        SettleTimeBetweenCapturesMs = config.SettleTimeBetweenCapturesMs;
        PowerPasteDelay = config.PowerPasteDelay;
        AltKeyRequiredForDragDrop = config.AltKeyRequiredForDragDrop;
        PayAttentionToClipboardIgnoreFlag = config.PayAttentionToClipboardIgnoreFlag;
        EnableCachedDatabaseWrites = config.EnableCachedDatabaseWrites;
        UseDdeForBrowserUrl = config.UseDdeForBrowserUrl;
        ReuseLastSelectedMoveTarget = config.ReuseLastSelectedMoveTarget;

        _logger.LogDebug("Advanced configuration loaded");
    }

    /// <summary>
    /// Saves advanced configuration.
    /// </summary>
    public void SaveAsync()
    {
        var config = _configurationService.Configuration.Preferences;

        config.StartupDelaySeconds = StartupDelaySeconds;
        config.CaptureDelayMs = CaptureDelayMs;
        config.SettleTimeBetweenCapturesMs = SettleTimeBetweenCapturesMs;
        config.PowerPasteDelay = PowerPasteDelay;
        config.AltKeyRequiredForDragDrop = AltKeyRequiredForDragDrop;
        config.PayAttentionToClipboardIgnoreFlag = PayAttentionToClipboardIgnoreFlag;
        config.EnableCachedDatabaseWrites = EnableCachedDatabaseWrites;
        config.UseDdeForBrowserUrl = UseDdeForBrowserUrl;
        config.ReuseLastSelectedMoveTarget = ReuseLastSelectedMoveTarget;

        _logger.LogDebug("Advanced configuration saved");
    }

    /// <summary>
    /// Resets layout and font settings to defaults.
    /// </summary>
    [RelayCommand]
    private void ResetLayoutFont()
    {
        var result = _dialogService.ShowMessage(
            "This will reset all layout and font settings to their defaults.\n\nA restart is required for the changes to take effect.\n\nDo you want to continue?",
            "Reset Layout/Font Settings",
            DialogButton.YesNo,
            DialogIcon.Warning);

        if (result != DialogResult.Yes)
            return;

        // TODO: Implement layout/font reset logic
        _logger.LogInformation("Layout/Font settings reset requested");

        _dialogService.ShowMessage(
            "Layout and font settings have been reset.\n\nPlease restart ClipMate for the changes to take effect.",
            "Settings Reset");
    }

    /// <summary>
    /// Clears the application profile.
    /// </summary>
    [RelayCommand]
    private void ResetApplicationProfile()
    {
        var result = _dialogService.ShowMessage(
            "This will clear all entries in the Application Profile, wiping it completely clean.\n\nA restart is required afterward.\n\nDo you want to continue?",
            "Clear Application Profile",
            DialogButton.YesNo,
            DialogIcon.Warning);

        if (result != DialogResult.Yes)
            return;

        // TODO: Implement application profile clear logic
        _logger.LogInformation("Application Profile reset requested");

        _dialogService.ShowMessage(
            "Application Profile has been cleared.\n\nPlease restart ClipMate for the changes to take effect.",
            "Application Profile Cleared");
    }

    /// <summary>
    /// Resets all settings to defaults.
    /// </summary>
    [RelayCommand]
    private void ResetEverything()
    {
        var result = _dialogService.ShowMessage(
            "This will reset ALL settings to their factory defaults.\n\nA restart is required for the changes to take effect.\n\nDo you want to continue?",
            "Reset All Settings",
            DialogButton.YesNo,
            DialogIcon.Warning);

        if (result != DialogResult.Yes)
            return;

        // TODO: Implement full settings reset logic
        _logger.LogInformation("Full settings reset requested");

        _dialogService.ShowMessage(
            "All settings have been reset to defaults.\n\nPlease restart ClipMate for the changes to take effect.",
            "Settings Reset");
    }
}
