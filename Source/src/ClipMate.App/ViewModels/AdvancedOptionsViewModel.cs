using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Platform;
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
    private readonly IApplicationProfileService _applicationProfileService;
    private readonly IConfigurationService _configurationService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<AdvancedOptionsViewModel> _logger;
    private readonly IStartupManager _startupManager;

    [ObservableProperty]
    private bool _altKeyRequiredForDragDrop;

    [ObservableProperty]
    private int _delayOnClipboardUpdateMs;

    [ObservableProperty]
    private bool _enableCachedDatabaseWrites;

    [ObservableProperty]
    private bool _payAttentionToClipboardIgnoreFlag;

    [ObservableProperty]
    private int _powerPasteDelay;

    [ObservableProperty]
    private bool _reuseLastSelectedMoveTarget;

    [ObservableProperty]
    private int _settleTimeMs;

    [ObservableProperty]
    private int _startupDelaySeconds;

    public AdvancedOptionsViewModel(IConfigurationService configurationService,
        IDialogService dialogService,
        IApplicationProfileService applicationProfileService,
        IStartupManager startupManager,
        ILogger<AdvancedOptionsViewModel> logger)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _applicationProfileService = applicationProfileService ?? throw new ArgumentNullException(nameof(applicationProfileService));
        _startupManager = startupManager ?? throw new ArgumentNullException(nameof(startupManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Loads advanced configuration.
    /// </summary>
    public void LoadAsync()
    {
        var config = _configurationService.Configuration.Preferences;

        StartupDelaySeconds = config.StartupDelaySeconds;
        DelayOnClipboardUpdateMs = config.DelayOnClipboardUpdateMs;
        SettleTimeMs = config.SettleTimeMs;
        PowerPasteDelay = config.PowerPasteDelay;
        AltKeyRequiredForDragDrop = config.AltKeyRequiredForDragDrop;
        PayAttentionToClipboardIgnoreFlag = config.PayAttentionToClipboardIgnoreFlag;
        EnableCachedDatabaseWrites = config.EnableCachedDatabaseWrites;
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
        config.DelayOnClipboardUpdateMs = DelayOnClipboardUpdateMs;
        config.SettleTimeMs = SettleTimeMs;
        config.PowerPasteDelay = PowerPasteDelay;
        config.AltKeyRequiredForDragDrop = AltKeyRequiredForDragDrop;
        config.PayAttentionToClipboardIgnoreFlag = PayAttentionToClipboardIgnoreFlag;
        config.EnableCachedDatabaseWrites = EnableCachedDatabaseWrites;
        config.ReuseLastSelectedMoveTarget = ReuseLastSelectedMoveTarget;

        _logger.LogDebug("Advanced configuration saved");
    }

    /// <summary>
    /// Raised when the Application Profiles have been cleared, so a tab holding them doesn't save them back.
    /// </summary>
    public event EventHandler? ApplicationProfilesCleared;

    /// <summary>
    /// Raised when Start With Windows has been turned off, so a tab holding the setting doesn't turn it back on.
    /// </summary>
    public event EventHandler? StartWithWindowsTurnedOff;

    /// <summary>
    /// Resets the position and size of ClipMate Classic and the editor font the next time ClipMate starts.
    /// </summary>
    [RelayCommand]
    private Task ResetLayoutFontAsync() =>
        RunResetAsync(
            "This will reset the position and size of ClipMate Classic and the editor font to their defaults the next time ClipMate starts.\n\nDo you want to continue?",
            "Reset Layout/Font Settings",
            () => RequestResetAsync(PendingSettingsReset.LayoutAndFonts),
            "The position and size of ClipMate Classic and the editor font will be reset the next time ClipMate starts.\n\nPlease restart ClipMate now.");

    /// <summary>
    /// Clears the Application Profiles. ClipMate reads them from their file for each capture, so this applies at once.
    /// </summary>
    [RelayCommand]
    private Task ResetApplicationProfileAsync() =>
        RunResetAsync(
            "This will clear all entries in the Application Profile, wiping it completely clean. ClipMate builds it again as you copy from each program.\n\nDo you want to continue?",
            "Clear Application Profile",
            ClearApplicationProfilesAsync,
            "Application Profile has been cleared.");

    /// <summary>
    /// Clears the Application Profiles and turns off Start With Windows now, since neither is held in the settings
    /// file, and resets every other setting the next time ClipMate starts.
    /// </summary>
    [RelayCommand]
    private Task ResetEverythingAsync() =>
        RunResetAsync(
            "This will clear the Application Profile and turn off Start with Windows now, and reset ALL other settings to their factory defaults the next time ClipMate starts.\n\nYour Databases and their clips, saved searches and paired Devices are kept.\n\nDo you want to continue?",
            "Reset All Settings",
            async () =>
            {
                await ClearApplicationProfilesAsync();
                await TurnOffStartWithWindowsAsync();
                await RequestResetAsync(PendingSettingsReset.Everything);
            },
            "All settings will be reset to defaults the next time ClipMate starts.\n\nPlease restart ClipMate now.");

    private async Task RunResetAsync(string confirmation, string title, Func<Task> reset, string doneMessage)
    {
        var result = _dialogService.ShowMessage(confirmation, title, DialogButton.YesNo, DialogIcon.Warning);
        if (result != DialogResult.Yes)
        {
            _logger.LogDebug("{Title}: not confirmed", title);
            return;
        }

        try
        {
            await reset();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Title} failed", title);
            _dialogService.ShowMessage($"The reset could not be finished.\n\n{ex.Message}", title, DialogButton.OK, DialogIcon.Error);
            return;
        }

        _logger.LogInformation("{Title} done", title);
        _dialogService.ShowMessage(doneMessage, title);
    }

    private async Task ClearApplicationProfilesAsync()
    {
        await _applicationProfileService.DeleteAllProfilesAsync();
        ApplicationProfilesCleared?.Invoke(this, EventArgs.Empty);
    }

    private async Task TurnOffStartWithWindowsAsync()
    {
        var (success, errorMessage) = await _startupManager.DisableAsync();
        if (!success)
            throw new InvalidOperationException(errorMessage);

        StartWithWindowsTurnedOff?.Invoke(this, EventArgs.Empty);
    }

    private async Task RequestResetAsync(PendingSettingsReset reset)
    {
        var configuration = _configurationService.Configuration;
        var previous = configuration.PendingSettingsReset;
        SettingsReset.Request(configuration, reset);

        try
        {
            await _configurationService.SaveAsync();
        }
        catch
        {
            // Reported as not done, so a later save must not carry it to the next start
            configuration.PendingSettingsReset = previous;
            throw;
        }
    }
}
