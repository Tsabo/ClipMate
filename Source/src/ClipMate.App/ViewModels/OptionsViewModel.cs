using System.Collections.ObjectModel;
using ClipMate.App.Views;
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
    private readonly IApplicationProfileService? _applicationProfileService;
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<OptionsViewModel> _logger;
    private readonly IMessenger _messenger;
    private readonly IStartupManager _startupManager;

    [ObservableProperty]
    private string _appendSeparatorString = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ApplicationProfileNode> _applicationProfileNodes = new();

    [ObservableProperty]
    private bool _autoChangeClipTitles;

    [ObservableProperty]
    private bool _autoExpandHdropFilePointers;

    [ObservableProperty]
    private bool _captureExistingClipboardAtStartup;

    [ObservableProperty]
    private bool _checkUpdatesAutomatically;

    [ObservableProperty]
    private CollectionIconClickBehavior _collectionIconClickBehavior;

    [ObservableProperty]
    private bool _confirmDeletionFromSafeCollections;

    [ObservableProperty]
    private bool _defaultAcceptClipsFromClipboard;

    [ObservableProperty]
    private EditorViewType _defaultEditorView;

    [ObservableProperty]
    private bool _displayWordAndCharacterCounts;

    [ObservableProperty]
    private string _editorFontFamily = "Consolas";

    [ObservableProperty]
    private int _editorFontSize;

    [ObservableProperty]
    private string _editorTheme = "vs-light";

    [ObservableProperty]
    private bool _editorWordWrap;

    // Application Profiles properties
    [ObservableProperty]
    private bool _enableApplicationProfiles;

    // Capturing tab properties
    [ObservableProperty]
    private bool _enableAutoCaptureAtStartup;

    // Editor tab properties (ClipViewer settings)
    [ObservableProperty]
    private bool _enableBinaryView;

    [ObservableProperty]
    private bool _enableDebugMode;

    [ObservableProperty]
    private ExplorerLayoutMode _explorerLayout;

    [ObservableProperty]
    private InitialShowMode _initialShowMode;

    // General tab properties
    [ObservableProperty]
    private bool _loadClassicAtStartup;

    [ObservableProperty]
    private bool _loadExplorerAtStartup;

    [ObservableProperty]
    private bool _mousewheelSelectsClip;

    // PowerPaste properties
    [ObservableProperty]
    private int _powerPasteDelay;

    [ObservableProperty]
    private string _powerPasteDelimiter = string.Empty;

    [ObservableProperty]
    private bool _powerPasteExplode;

    [ObservableProperty]
    private bool _powerPasteIncludeDelimiter;

    [ObservableProperty]
    private bool _powerPasteLoop;

    [ObservableProperty]
    private bool _powerPasteShield;

    [ObservableProperty]
    private bool _powerPasteTrim;

    // QuickPaste properties
    [ObservableProperty]
    private bool _quickPasteAutoTargetingEnabled;

    [ObservableProperty]
    private ObservableCollection<string> _quickPasteBadTargets = new();

    [ObservableProperty]
    private ObservableCollection<QuickPasteFormattingString> _quickPasteFormattingStrings = new();

    [ObservableProperty]
    private ObservableCollection<string> _quickPasteGoodTargets = new();

    [ObservableProperty]
    private bool _quickPastePasteOnDoubleClick;

    [ObservableProperty]
    private bool _quickPastePasteOnEnter;

    [ObservableProperty]
    private int _selectedTabIndex;

    // Editor tab properties (Monaco Editor settings)
    [ObservableProperty]
    private bool _showLineNumbersInEditor;

    [ObservableProperty]
    private bool _showMinimap;

    [ObservableProperty]
    private bool _showToolbar;

    [ObservableProperty]
    private bool _smoothScrolling;

    [ObservableProperty]
    private bool _sortCollectionsAlphabetically;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _stripTrailingLineBreak;

    [ObservableProperty]
    private int _tabStops;

    [ObservableProperty]
    private int _updateCheckIntervalDays;

    public OptionsViewModel(IConfigurationService configurationService,
        IStartupManager startupManager,
        IMessenger messenger,
        ILogger<OptionsViewModel> logger,
        IApplicationProfileService? applicationProfileService = null)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _startupManager = startupManager ?? throw new ArgumentNullException(nameof(startupManager));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _applicationProfileService = applicationProfileService;

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

        // QuickPaste tab
        QuickPasteAutoTargetingEnabled = config.QuickPasteAutoTargetingEnabled;
        QuickPastePasteOnEnter = config.QuickPastePasteOnEnter;
        QuickPastePasteOnDoubleClick = config.QuickPastePasteOnDoubleClick;
        QuickPasteGoodTargets = new ObservableCollection<string>(config.QuickPasteGoodTargets);
        QuickPasteBadTargets = new ObservableCollection<string>(config.QuickPasteBadTargets);
        QuickPasteFormattingStrings = new ObservableCollection<QuickPasteFormattingString>(config.QuickPasteFormattingStrings);

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
            StartWithWindows = isEnabled;
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

        // Application Profiles tab (session-only setting)
        if (_applicationProfileService != null)
        {
            EnableApplicationProfiles = _applicationProfileService.IsApplicationProfilesEnabled();
            await LoadApplicationProfilesAsync();
        }

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

            // QuickPaste tab
            config.Preferences.QuickPasteAutoTargetingEnabled = QuickPasteAutoTargetingEnabled;
            config.Preferences.QuickPastePasteOnEnter = QuickPastePasteOnEnter;
            config.Preferences.QuickPastePasteOnDoubleClick = QuickPastePasteOnDoubleClick;
            config.Preferences.QuickPasteGoodTargets = QuickPasteGoodTargets.ToList();
            config.Preferences.QuickPasteBadTargets = QuickPasteBadTargets.ToList();
            config.Preferences.QuickPasteFormattingStrings = QuickPasteFormattingStrings.ToList();

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

            // Application Profiles (session-only setting, not persisted to config)
            if (_applicationProfileService != null)
            {
                _applicationProfileService.SetApplicationProfilesEnabled(EnableApplicationProfiles);

                // Save updated profile states back to storage
                foreach (var profileNode in ApplicationProfileNodes)
                    await _applicationProfileService.UpdateProfileAsync(profileNode.Profile);
            }

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

            // Broadcast QuickPaste configuration changed event for immediate reload
            _messenger.Send(new QuickPasteConfigurationChangedEvent());

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
    /// Loads all application profiles from the service.
    /// </summary>
    [RelayCommand]
    private async Task LoadApplicationProfilesAsync()
    {
        if (_applicationProfileService == null)
        {
            _logger.LogWarning("Application profile service not available");
            return;
        }

        try
        {
            var profiles = await _applicationProfileService.GetAllProfilesAsync();
            ApplicationProfileNodes.Clear();

            foreach (var kvp in profiles.OrderBy(p => p.Key))
            {
                var profileNode = new ApplicationProfileNode(kvp.Value);
                ApplicationProfileNodes.Add(profileNode);
            }

            _logger.LogInformation("Loaded {Count} application profiles", ApplicationProfileNodes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load application profiles");
        }
    }

    /// <summary>
    /// Deletes all application profiles.
    /// </summary>
    [RelayCommand]
    private async Task DeleteAllProfilesAsync()
    {
        if (_applicationProfileService == null)
        {
            _logger.LogWarning("Application profile service not available");
            return;
        }

        try
        {
            await _applicationProfileService.DeleteAllProfilesAsync();
            ApplicationProfileNodes.Clear();
            _logger.LogInformation("Deleted all application profiles");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete application profiles");
        }
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

    #region QuickPaste Commands

    /// <summary>
    /// Adds a new GOOD target specification.
    /// </summary>
    [RelayCommand]
    private void AddGoodTarget()
    {
        var dialog = new QuickPasteTargetDialog();
        if (dialog.ShowDialog() == true)
        {
            var target = dialog.TargetSpecification;
            if (!string.IsNullOrWhiteSpace(target) && !QuickPasteGoodTargets.Contains(target))
            {
                QuickPasteGoodTargets.Add(target);
                _logger.LogDebug("Added GOOD target: {Target}", target);
            }
        }
    }

    /// <summary>
    /// Edits the selected GOOD target specification.
    /// </summary>
    [RelayCommand]
    private void EditGoodTarget()
    {
        // TODO: Get selected item from grid
        _logger.LogInformation("Edit GOOD target requested");
    }

    /// <summary>
    /// Deletes the selected GOOD target specification.
    /// </summary>
    [RelayCommand]
    private void DeleteGoodTarget()
    {
        // TODO: Get selected item from grid and remove
        _logger.LogInformation("Delete GOOD target requested");
    }

    /// <summary>
    /// Adds a new BAD target specification.
    /// </summary>
    [RelayCommand]
    private void AddBadTarget()
    {
        var dialog = new QuickPasteTargetDialog();
        if (dialog.ShowDialog() == true)
        {
            var target = dialog.TargetSpecification;
            if (!string.IsNullOrWhiteSpace(target) && !QuickPasteBadTargets.Contains(target))
            {
                QuickPasteBadTargets.Add(target);
                _logger.LogDebug("Added BAD target: {Target}", target);
            }
        }
    }

    /// <summary>
    /// Edits the selected BAD target specification.
    /// </summary>
    [RelayCommand]
    private void EditBadTarget()
    {
        // TODO: Get selected item from grid
        _logger.LogInformation("Edit BAD target requested");
    }

    /// <summary>
    /// Deletes the selected BAD target specification.
    /// </summary>
    [RelayCommand]
    private void DeleteBadTarget()
    {
        // TODO: Get selected item from grid and remove
        _logger.LogInformation("Delete BAD target requested");
    }

    /// <summary>
    /// Adds a new formatting string.
    /// </summary>
    [RelayCommand]
    private void AddFormattingString()
    {
        var dialog = new QuickPasteFormattingStringDialog();
        if (dialog.ShowDialog() == true && dialog.FormattingString != null)
        {
            QuickPasteFormattingStrings.Add(dialog.FormattingString);
            _logger.LogDebug("Added formatting string: {Title}", dialog.FormattingString.Title);
        }
    }

    /// <summary>
    /// Edits the selected formatting string.
    /// </summary>
    [RelayCommand]
    private void EditFormattingString()
    {
        // TODO: Get selected item from grid
        _logger.LogInformation("Edit formatting string requested");
    }

    /// <summary>
    /// Deletes the selected formatting string.
    /// </summary>
    [RelayCommand]
    private void DeleteFormattingString()
    {
        // TODO: Get selected item from grid and remove
        _logger.LogInformation("Delete formatting string requested");
    }

    #endregion
}
