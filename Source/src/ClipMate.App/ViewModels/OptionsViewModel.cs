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
/// ViewModel for the Options dialog.
/// </summary>
public partial class OptionsViewModel : ObservableObject
{
    private readonly IConfigurationService _configurationService;
    private readonly IStartupManager _startupManager;
    private readonly IMessenger _messenger;
    private readonly ILogger<OptionsViewModel> _logger;

    [ObservableProperty]
    private int _selectedTabIndex;

    // PowerPaste properties
    [ObservableProperty]
    private int _powerPasteDelay;

    [ObservableProperty]
    private bool _powerPasteShield;

    [ObservableProperty]
    private string _powerPasteDelimiter = string.Empty;

    [ObservableProperty]
    private bool _powerPasteTrim;

    [ObservableProperty]
    private bool _powerPasteIncludeDelimiter;

    [ObservableProperty]
    private bool _powerPasteLoop;

    [ObservableProperty]
    private bool _powerPasteExplode;

    // General tab properties
    [ObservableProperty]
    private bool _loadClassicAtStartup;

    [ObservableProperty]
    private bool _loadExplorerAtStartup;

    [ObservableProperty]
    private InitialShowMode _initialShowMode;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _confirmDeletionFromSafeCollections;

    [ObservableProperty]
    private bool _checkUpdatesAutomatically;

    [ObservableProperty]
    private int _updateCheckIntervalDays;

    [ObservableProperty]
    private bool _sortCollectionsAlphabetically;

    [ObservableProperty]
    private bool _mousewheelSelectsClip;

    [ObservableProperty]
    private CollectionIconClickBehavior _collectionIconClickBehavior;

    [ObservableProperty]
    private ExplorerLayoutMode _explorerLayout;

    // Editor tab properties (Monaco Editor settings)
    [ObservableProperty]
    private bool _showLineNumbersInEditor;

    [ObservableProperty]
    private bool _displayWordAndCharacterCounts;

    [ObservableProperty]
    private bool _smoothScrolling;

    [ObservableProperty]
    private int _tabStops;

    [ObservableProperty]
    private string _editorTheme = "vs-light";

    [ObservableProperty]
    private int _editorFontSize;

    [ObservableProperty]
    private string _editorFontFamily = "Consolas";

    [ObservableProperty]
    private bool _editorWordWrap;

    [ObservableProperty]
    private bool _showMinimap;

    [ObservableProperty]
    private bool _showToolbar;

    [ObservableProperty]
    private bool _enableDebugMode;

    // Editor tab properties (ClipViewer settings)
    [ObservableProperty]
    private bool _enableBinaryView;

    [ObservableProperty]
    private bool _autoChangeClipTitles;

    [ObservableProperty]
    private EditorViewType _defaultEditorView;

    // Capturing tab properties
    [ObservableProperty]
    private bool _enableAutoCaptureAtStartup;

    [ObservableProperty]
    private bool _captureExistingClipboardAtStartup;

    [ObservableProperty]
    private bool _autoExpandHdropFilePointers;

    [ObservableProperty]
    private bool _defaultAcceptClipsFromClipboard;

    [ObservableProperty]
    private string _appendSeparatorString = string.Empty;

    [ObservableProperty]
    private bool _stripTrailingLineBreak;

    public OptionsViewModel(
        IConfigurationService configurationService,
        IStartupManager startupManager,
        IMessenger messenger,
        ILogger<OptionsViewModel> logger)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _startupManager = startupManager ?? throw new ArgumentNullException(nameof(startupManager));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Note: LoadConfigurationAsync() will be called from the View's Loaded event
    }

    /// <summary>
    /// Loads configuration from the configuration service.
    /// </summary>
    public async Task LoadConfigurationAsync()
    {
        var config = _configurationService.Configuration.Preferences;

        // PowerPaste tab
        PowerPasteDelay = config.PowerPasteDelay;
        PowerPasteShield = config.PowerPasteShield;
        PowerPasteDelimiter = config.PowerPasteDelimiter;
        PowerPasteTrim = config.PowerPasteTrim;
        PowerPasteIncludeDelimiter = config.PowerPasteIncludeDelimiter;
        PowerPasteLoop = config.PowerPasteLoop;
        PowerPasteExplode = config.PowerPasteExplode;

        // General tab
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

        // Load the current startup state from registry
        var (success, isEnabled, errorMessage) = await _startupManager.IsEnabledAsync();
        if (success)
        {
            StartWithWindows = isEnabled;
        }
        else
        {
            _logger.LogWarning("Failed to check startup status: {ErrorMessage}", errorMessage);
            StartWithWindows = config.StartWithWindows; // Fallback to saved preference
        }

        // Editor tab - Monaco settings
        var monacoConfig = _configurationService.Configuration.MonacoEditor;
        ShowLineNumbersInEditor = monacoConfig.ShowLineNumbers;
        DisplayWordAndCharacterCounts = monacoConfig.DisplayWordAndCharacterCounts;
        SmoothScrolling = monacoConfig.SmoothScrolling;
        TabStops = monacoConfig.TabSize;
        EditorTheme = monacoConfig.Theme;
        EditorFontSize = monacoConfig.FontSize;
        EditorFontFamily = monacoConfig.FontFamily;
        EditorWordWrap = monacoConfig.WordWrap;
        ShowMinimap = monacoConfig.ShowMinimap;
        ShowToolbar = monacoConfig.ShowToolbar;
        EnableDebugMode = monacoConfig.EnableDebug;

        // Editor tab - ClipViewer settings
        EnableBinaryView = config.EnableBinaryView;
        AutoChangeClipTitles = config.AutoChangeClipTitles;
        DefaultEditorView = config.DefaultEditorView;

        // Capturing tab
        EnableAutoCaptureAtStartup = config.EnableAutoCaptureAtStartup;
        CaptureExistingClipboardAtStartup = config.CaptureExistingClipboardAtStartup;
        AutoExpandHdropFilePointers = config.AutoExpandHdropFilePointers;
        DefaultAcceptClipsFromClipboard = config.DefaultAcceptClipsFromClipboard;
        AppendSeparatorString = config.AppendSeparatorString;
        StripTrailingLineBreak = config.StripTrailingLineBreak;

        _logger.LogDebug("Configuration loaded into OptionsViewModel");
    }

    /// <summary>
    /// Saves the configuration to the configuration service.
    /// </summary>
    [RelayCommand]
    private async Task OkAsync()
    {
        try
        {
            // Update configuration
            var config = _configurationService.Configuration;
            
            // PowerPaste tab
            config.Preferences.PowerPasteDelay = PowerPasteDelay;
            config.Preferences.PowerPasteShield = PowerPasteShield;
            config.Preferences.PowerPasteDelimiter = PowerPasteDelimiter;
            config.Preferences.PowerPasteTrim = PowerPasteTrim;
            config.Preferences.PowerPasteIncludeDelimiter = PowerPasteIncludeDelimiter;
            config.Preferences.PowerPasteLoop = PowerPasteLoop;
            config.Preferences.PowerPasteExplode = PowerPasteExplode;

            // General tab
            config.Preferences.LoadClassicAtStartup = LoadClassicAtStartup;
            config.Preferences.LoadExplorerAtStartup = LoadExplorerAtStartup;
            config.Preferences.InitialShowMode = InitialShowMode;
            config.Preferences.StartWithWindows = StartWithWindows;
            config.Preferences.ConfirmDeletionFromSafeCollections = ConfirmDeletionFromSafeCollections;
            config.Preferences.CheckUpdatesAutomatically = CheckUpdatesAutomatically;
            config.Preferences.UpdateCheckIntervalDays = UpdateCheckIntervalDays;
            config.Preferences.SortCollectionsAlphabetically = SortCollectionsAlphabetically;
            config.Preferences.MousewheelSelectsClip = MousewheelSelectsClip;
            config.Preferences.CollectionIconClickBehavior = CollectionIconClickBehavior;
            config.Preferences.ExplorerLayout = ExplorerLayout;

            // Editor tab - Monaco Editor settings
            var monacoConfig = _configurationService.Configuration.MonacoEditor;
            monacoConfig.ShowLineNumbers = ShowLineNumbersInEditor;
            monacoConfig.DisplayWordAndCharacterCounts = DisplayWordAndCharacterCounts;
            monacoConfig.SmoothScrolling = SmoothScrolling;
            monacoConfig.TabSize = TabStops;
            monacoConfig.Theme = EditorTheme;
            monacoConfig.FontSize = EditorFontSize;
            monacoConfig.FontFamily = EditorFontFamily;
            monacoConfig.WordWrap = EditorWordWrap;
            monacoConfig.ShowMinimap = ShowMinimap;
            monacoConfig.ShowToolbar = ShowToolbar;
            monacoConfig.EnableDebug = EnableDebugMode;

            // Editor tab - ClipViewer settings
            config.Preferences.EnableBinaryView = EnableBinaryView;
            config.Preferences.AutoChangeClipTitles = AutoChangeClipTitles;
            config.Preferences.DefaultEditorView = DefaultEditorView;

            // Capturing tab
            config.Preferences.EnableAutoCaptureAtStartup = EnableAutoCaptureAtStartup;
            config.Preferences.CaptureExistingClipboardAtStartup = CaptureExistingClipboardAtStartup;
            config.Preferences.AutoExpandHdropFilePointers = AutoExpandHdropFilePointers;
            config.Preferences.DefaultAcceptClipsFromClipboard = DefaultAcceptClipsFromClipboard;
            config.Preferences.AppendSeparatorString = AppendSeparatorString;
            config.Preferences.StripTrailingLineBreak = StripTrailingLineBreak;

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

            // Save to disk
            await _configurationService.SaveAsync();

            // Broadcast preferences changed event
            _messenger.Send(new PreferencesChangedEvent());

            _logger.LogInformation("Configuration saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration");
            throw;
        }
    }

    /// <summary>
    /// Cancels changes and reverts to saved configuration.
    /// </summary>
    [RelayCommand]
    private async Task CancelAsync()
    {
        // Reload configuration to discard changes
        await LoadConfigurationAsync();
        _logger.LogDebug("Configuration changes cancelled");
    }

    /// <summary>
    /// Shows help for the Options dialog.
    /// </summary>
    [RelayCommand]
    private void Help()
    {
        // TODO: Implement help system
        _logger.LogInformation("Help requested for Options dialog");
    }

    /// <summary>
    /// Resets PowerPaste settings to defaults.
    /// </summary>
    [RelayCommand]
    private void ResetPowerPasteToDefaults()
    {
        PowerPasteDelay = 100;
        PowerPasteShield = true;
        PowerPasteDelimiter = ",.;:\\n\\t";
        PowerPasteTrim = true;
        PowerPasteIncludeDelimiter = false;
        PowerPasteLoop = false;
        PowerPasteExplode = false;

        _logger.LogInformation("PowerPaste settings reset to defaults");
    }

    /// <summary>
    /// Resets General tab settings to defaults.
    /// </summary>
    [RelayCommand]
    private void ResetGeneralToDefaults()
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

        _logger.LogInformation("General settings reset to defaults");
    }

    /// <summary>
    /// Resets Editor tab settings to defaults.
    /// </summary>
    [RelayCommand]
    private void ResetEditorToDefaults()
    {
        // Monaco Editor defaults
        ShowLineNumbersInEditor = true;
        DisplayWordAndCharacterCounts = true;
        SmoothScrolling = true;
        TabStops = 4;
        EditorTheme = "vs-light";
        EditorFontSize = 14;
        EditorFontFamily = "Consolas";
        EditorWordWrap = true;
        ShowMinimap = false;
        ShowToolbar = true;
        EnableDebugMode = false;

        // ClipViewer defaults
        EnableBinaryView = true;
        AutoChangeClipTitles = false;
        DefaultEditorView = EditorViewType.Text;

        _logger.LogInformation("Editor settings reset to defaults");
    }

    /// <summary>
    /// Resets Capturing tab settings to defaults.
    /// </summary>
    [RelayCommand]
    private void ResetCapturingToDefaults()
    {
        EnableAutoCaptureAtStartup = true;
        CaptureExistingClipboardAtStartup = true;
        AutoExpandHdropFilePointers = true;
        DefaultAcceptClipsFromClipboard = true;
        AppendSeparatorString = "\r\n";
        StripTrailingLineBreak = false;

        _logger.LogInformation("Capturing settings reset to defaults");
    }
}
