using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the Capturing options tab.
/// </summary>
public partial class CapturingOptionsViewModel : ObservableObject
{
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<CapturingOptionsViewModel> _logger;

    [ObservableProperty]
    private string _appendSeparatorString = string.Empty;

    [ObservableProperty]
    private bool _autoExpandHdropFilePointers;

    [ObservableProperty]
    private bool _captureExistingClipboardAtStartup;

    [ObservableProperty]
    private bool _defaultAcceptClipsFromClipboard;

    [ObservableProperty]
    private bool _enableAutoCaptureAtStartup;

    [ObservableProperty]
    private bool _stripTrailingLineBreak;

    public CapturingOptionsViewModel(
        IConfigurationService configurationService,
        ILogger<CapturingOptionsViewModel> logger)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Loads capturing configuration.
    /// </summary>
    public void LoadAsync()
    {
        var config = _configurationService.Configuration.Preferences;

        EnableAutoCaptureAtStartup = config.EnableAutoCaptureAtStartup;
        CaptureExistingClipboardAtStartup = config.CaptureExistingClipboardAtStartup;
        AutoExpandHdropFilePointers = config.AutoExpandHdropFilePointers;
        DefaultAcceptClipsFromClipboard = config.DefaultAcceptClipsFromClipboard;
        AppendSeparatorString = config.AppendSeparatorString;
        StripTrailingLineBreak = config.StripTrailingLineBreak;

        _logger.LogDebug("Capturing configuration loaded");
    }

    /// <summary>
    /// Saves capturing configuration.
    /// </summary>
    public void SaveAsync()
    {
        var config = _configurationService.Configuration.Preferences;

        config.EnableAutoCaptureAtStartup = EnableAutoCaptureAtStartup;
        config.CaptureExistingClipboardAtStartup = CaptureExistingClipboardAtStartup;
        config.AutoExpandHdropFilePointers = AutoExpandHdropFilePointers;
        config.DefaultAcceptClipsFromClipboard = DefaultAcceptClipsFromClipboard;
        config.AppendSeparatorString = AppendSeparatorString;
        config.StripTrailingLineBreak = StripTrailingLineBreak;

        _logger.LogDebug("Capturing configuration saved");
    }

    /// <summary>
    /// Resets Capturing tab settings to defaults.
    /// </summary>
    [RelayCommand]
    private void ResetToDefaults()
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
