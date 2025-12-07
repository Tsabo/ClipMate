using ClipMate.Core.Events;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the General options tab.
/// </summary>
public partial class GeneralOptionsViewModel : ObservableObject
{
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<GeneralOptionsViewModel> _logger;
    private readonly IMessenger _messenger;
    private readonly IStartupManager _startupManager;

    [ObservableProperty]
    private bool _checkUpdatesAutomatically;

    [ObservableProperty]
    private ClipBarPopupLocation _clipBarPopupLocation;

    [ObservableProperty]
    private CollectionIconClickBehavior _collectionIconClickBehavior;

    [ObservableProperty]
    private bool _confirmDeletionFromSafeCollections;

    [ObservableProperty]
    private ExplorerLayoutMode _explorerLayout;

    [ObservableProperty]
    private InitialShowMode _initialShowMode;

    [ObservableProperty]
    private bool _loadClassicAtStartup;

    [ObservableProperty]
    private bool _loadExplorerAtStartup;

    [ObservableProperty]
    private bool _mousewheelSelectsClip;

    [ObservableProperty]
    private bool _showTaskbarIcon;

    [ObservableProperty]
    private bool _showTrayIcon;

    [ObservableProperty]
    private bool _sortCollectionsAlphabetically;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private IconLeftClickAction _taskbarIconLeftClickAction;

    [ObservableProperty]
    private IconLeftClickAction _trayIconLeftClickAction;

    [ObservableProperty]
    private int _updateCheckIntervalDays;

    public GeneralOptionsViewModel(IConfigurationService configurationService,
        IStartupManager startupManager,
        IMessenger messenger,
        ILogger<GeneralOptionsViewModel> logger)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _startupManager = startupManager ?? throw new ArgumentNullException(nameof(startupManager));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Loads general configuration.
    /// </summary>
    public async Task LoadAsync()
    {
        var config = _configurationService.Configuration.Preferences;

        LoadClassicAtStartup = config.LoadClassicAtStartup;
        LoadExplorerAtStartup = config.LoadExplorerAtStartup;
        InitialShowMode = config.InitialShowMode;
        ConfirmDeletionFromSafeCollections = config.ConfirmDeletionFromSafeCollections;
        CheckUpdatesAutomatically = config.CheckUpdatesAutomatically;
        UpdateCheckIntervalDays = config.UpdateCheckIntervalDays;
        SortCollectionsAlphabetically = config.SortCollectionsAlphabetically;
        MousewheelSelectsClip = config.MousewheelSelectsClip;
        CollectionIconClickBehavior = config.CollectionIconClickBehavior;
        ExplorerLayout = config.ExplorerLayout;
        ShowTrayIcon = config.ShowTrayIcon;
        ShowTaskbarIcon = config.ShowTaskbarIcon;
        TrayIconLeftClickAction = config.TrayIconLeftClickAction;
        TaskbarIconLeftClickAction = config.TaskbarIconLeftClickAction;
        ClipBarPopupLocation = config.ClipBarPopupLocation;

        // Load the current startup state from registry
        var (success, isEnabled, errorMessage) = await _startupManager.IsEnabledAsync();
        if (success)
            StartWithWindows = isEnabled;
        else
        {
            _logger.LogWarning("Failed to check startup status: {ErrorMessage}", errorMessage);
            StartWithWindows = config.StartWithWindows; // Fallback to saved preference
        }

        _logger.LogDebug("General configuration loaded");
    }

    /// <summary>
    /// Saves general configuration.
    /// </summary>
    public async Task SaveAsync()
    {
        var config = _configurationService.Configuration.Preferences;

        config.LoadClassicAtStartup = LoadClassicAtStartup;
        config.LoadExplorerAtStartup = LoadExplorerAtStartup;
        config.InitialShowMode = InitialShowMode;
        config.StartWithWindows = StartWithWindows;
        config.ConfirmDeletionFromSafeCollections = ConfirmDeletionFromSafeCollections;
        config.CheckUpdatesAutomatically = CheckUpdatesAutomatically;
        config.UpdateCheckIntervalDays = UpdateCheckIntervalDays;
        config.SortCollectionsAlphabetically = SortCollectionsAlphabetically;
        config.MousewheelSelectsClip = MousewheelSelectsClip;
        config.CollectionIconClickBehavior = CollectionIconClickBehavior;
        config.ExplorerLayout = ExplorerLayout;

        // Validate icon visibility - at least one must be visible
        if (!ShowTrayIcon && !ShowTaskbarIcon)
        {
            _logger.LogWarning("Cannot hide both tray icon and taskbar icon, forcing tray icon visible");
            ShowTrayIcon = true;
        }

        config.ShowTrayIcon = ShowTrayIcon;
        config.ShowTaskbarIcon = ShowTaskbarIcon;
        config.TrayIconLeftClickAction = TrayIconLeftClickAction;
        config.TaskbarIconLeftClickAction = TaskbarIconLeftClickAction;
        config.ClipBarPopupLocation = ClipBarPopupLocation;

        // Send events to update UI components
        _messenger.Send(new ShowTrayIconChangedEvent(ShowTrayIcon));
        _messenger.Send(new ShowTaskbarIconChangedEvent(ShowTaskbarIcon));
        _messenger.Send(new IconClickBehaviorChangedEvent());

        // Handle Windows startup registry setting
        if (StartWithWindows)
        {
            var (enableSuccess, enableError) = await _startupManager.EnableAsync();
            if (!enableSuccess)
            {
                _logger.LogError("Failed to enable Windows startup: {Error}", enableError);
                // TODO: Show user-friendly notification using DevExpress toast/notification API
            }
        }
        else
        {
            var (disableSuccess, disableError) = await _startupManager.DisableAsync();
            if (!disableSuccess)
            {
                _logger.LogError("Failed to disable Windows startup: {Error}", disableError);
                // TODO: Show user-friendly notification using DevExpress toast/notification API
            }
        }

        _logger.LogDebug("General configuration saved");
    }

    /// <summary>
    /// Resets General tab settings to defaults.
    /// </summary>
    [RelayCommand]
    private void ResetToDefaults()
    {
        LoadClassicAtStartup = false;
        LoadExplorerAtStartup = false;
        InitialShowMode = InitialShowMode.Nothing;
        StartWithWindows = false;
        ConfirmDeletionFromSafeCollections = true;
        CheckUpdatesAutomatically = true;
        UpdateCheckIntervalDays = 5;
        SortCollectionsAlphabetically = false;
        MousewheelSelectsClip = true;
        CollectionIconClickBehavior = CollectionIconClickBehavior.MenuAppears;
        ExplorerLayout = ExplorerLayoutMode.FullWidthEditor;
        ShowTrayIcon = true;
        ShowTaskbarIcon = true;
        TrayIconLeftClickAction = IconLeftClickAction.ShowExplorerWindow;
        TaskbarIconLeftClickAction = IconLeftClickAction.ShowExplorerWindow;
        ClipBarPopupLocation = ClipBarPopupLocation.AtMouseCursor;

        _logger.LogInformation("General settings reset to defaults");
    }
}
