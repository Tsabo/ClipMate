using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the Appearance options tab.
/// </summary>
public partial class AppearanceOptionsViewModel : ObservableObject
{
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<AppearanceOptionsViewModel> _logger;

    [ObservableProperty]
    private AppTheme _appTheme;

    [ObservableProperty]
    private bool _monacoThemeFollowsAppTheme;

    public AppearanceOptionsViewModel(IConfigurationService configurationService,
        ILogger<AppearanceOptionsViewModel> logger)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Loads appearance configuration.
    /// </summary>
    public void LoadAsync()
    {
        var config = _configurationService.Configuration.Appearance;

        AppTheme = config.AppTheme;
        MonacoThemeFollowsAppTheme = config.MonacoThemeFollowsAppTheme;

        _logger.LogDebug("Appearance configuration loaded");
    }

    /// <summary>
    /// Saves appearance configuration.
    /// </summary>
    public void SaveAsync()
    {
        var config = _configurationService.Configuration.Appearance;

        config.AppTheme = AppTheme;
        config.MonacoThemeFollowsAppTheme = MonacoThemeFollowsAppTheme;

        _logger.LogDebug("Appearance configuration saved");
    }

    /// <summary>
    /// Resets Appearance tab settings to defaults.
    /// </summary>
    [RelayCommand]
    private void ResetToDefaults()
    {
        AppTheme = AppTheme.Light;
        MonacoThemeFollowsAppTheme = true;

        _logger.LogInformation("Appearance settings reset to defaults");
    }
}
