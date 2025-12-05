using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the Editor options tab.
/// </summary>
public partial class EditorOptionsViewModel : ObservableObject
{
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<EditorOptionsViewModel> _logger;

    // ClipViewer properties
    [ObservableProperty]
    private bool _autoChangeClipTitles;

    [ObservableProperty]
    private EditorViewType _defaultEditorView;

    [ObservableProperty]
    private bool _displayWordAndCharacterCounts;

    // Monaco Editor properties
    [ObservableProperty]
    private string _editorFontFamily = "Consolas";

    [ObservableProperty]
    private int _editorFontSize;

    [ObservableProperty]
    private string _editorTheme = "vs-light";

    [ObservableProperty]
    private bool _editorWordWrap;

    [ObservableProperty]
    private bool _enableBinaryView;

    [ObservableProperty]
    private bool _enableDebugMode;

    [ObservableProperty]
    private bool _showLineNumbersInEditor;

    [ObservableProperty]
    private bool _showMinimap;

    [ObservableProperty]
    private bool _showToolbar;

    [ObservableProperty]
    private bool _smoothScrolling;

    [ObservableProperty]
    private int _tabStops;

    public EditorOptionsViewModel(
        IConfigurationService configurationService,
        ILogger<EditorOptionsViewModel> logger)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Loads editor configuration.
    /// </summary>
    public void LoadAsync()
    {
        var config = _configurationService.Configuration.Preferences;
        var monacoConfig = _configurationService.Configuration.MonacoEditor;

        // Monaco settings
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

        // ClipViewer settings
        EnableBinaryView = config.EnableBinaryView;
        AutoChangeClipTitles = config.AutoChangeClipTitles;
        DefaultEditorView = config.DefaultEditorView;

        _logger.LogDebug("Editor configuration loaded");
    }

    /// <summary>
    /// Saves editor configuration.
    /// </summary>
    public void SaveAsync()
    {
        var config = _configurationService.Configuration.Preferences;
        var monacoConfig = _configurationService.Configuration.MonacoEditor;

        // Monaco Editor settings
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

        // ClipViewer settings
        config.EnableBinaryView = EnableBinaryView;
        config.AutoChangeClipTitles = AutoChangeClipTitles;
        config.DefaultEditorView = DefaultEditorView;

        _logger.LogDebug("Editor configuration saved");
    }

    /// <summary>
    /// Resets Editor tab settings to defaults.
    /// </summary>
    [RelayCommand]
    private void ResetToDefaults()
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
}
