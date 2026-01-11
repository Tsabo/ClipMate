using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the PowerPaste options tab.
/// </summary>
public partial class PowerPasteOptionsViewModel : ObservableObject
{
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<PowerPasteOptionsViewModel> _logger;

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

    public PowerPasteOptionsViewModel(IConfigurationService configurationService,
        ILogger<PowerPasteOptionsViewModel> logger)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Loads PowerPaste configuration.
    /// </summary>
    public void LoadAsync()
    {
        var config = _configurationService.Configuration.Preferences;

        PowerPasteShield = config.PowerPasteShield;
        PowerPasteDelimiter = config.PowerPasteDelimiter;
        PowerPasteTrim = config.PowerPasteTrim;
        PowerPasteIncludeDelimiter = config.PowerPasteIncludeDelimiter;
        PowerPasteLoop = config.PowerPasteLoop;
        PowerPasteExplode = config.PowerPasteExplode;

        _logger.LogDebug("PowerPaste configuration loaded");
    }

    /// <summary>
    /// Saves PowerPaste configuration.
    /// </summary>
    public void SaveAsync()
    {
        var config = _configurationService.Configuration.Preferences;

        config.PowerPasteShield = PowerPasteShield;
        config.PowerPasteDelimiter = PowerPasteDelimiter;
        config.PowerPasteTrim = PowerPasteTrim;
        config.PowerPasteIncludeDelimiter = PowerPasteIncludeDelimiter;
        config.PowerPasteLoop = PowerPasteLoop;
        config.PowerPasteExplode = PowerPasteExplode;

        _logger.LogDebug("PowerPaste configuration saved");
    }

    /// <summary>
    /// Resets PowerPaste settings to defaults.
    /// </summary>
    [RelayCommand]
    private void ResetToDefaults()
    {
        PowerPasteShield = true;
        PowerPasteDelimiter = ",.;:\\n\\t";
        PowerPasteTrim = true;
        PowerPasteIncludeDelimiter = false;
        PowerPasteLoop = false;
        PowerPasteExplode = false;

        _logger.LogInformation("PowerPaste settings reset to defaults");
    }
}
