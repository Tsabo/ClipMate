using ClipMate.Core.Services.Abstractions;
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
    private bool _powerPasteTrim;

    public PowerPasteOptionsViewModel(IConfigurationService configurationService,
        OutboundFilterOptionsViewModel outboundFilter,
        ILogger<PowerPasteOptionsViewModel> logger)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        OutboundFilter = outboundFilter ?? throw new ArgumentNullException(nameof(outboundFilter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// The Outbound Clip Filtering section, on the same Pasting page.
    /// </summary>
    public OutboundFilterOptionsViewModel OutboundFilter { get; }

    /// <summary>
    /// Loads PowerPaste configuration.
    /// </summary>
    public void LoadAsync()
    {
        var config = _configurationService.Configuration.Preferences;

        PowerPasteDelimiter = config.PowerPasteDelimiter;
        PowerPasteTrim = config.PowerPasteTrim;
        PowerPasteIncludeDelimiter = config.PowerPasteIncludeDelimiter;
        PowerPasteLoop = config.PowerPasteLoop;
        PowerPasteExplode = config.PowerPasteExplode;
        OutboundFilter.Load();

        _logger.LogDebug("PowerPaste configuration loaded");
    }

    /// <summary>
    /// Saves PowerPaste configuration.
    /// </summary>
    public void SaveAsync()
    {
        var config = _configurationService.Configuration.Preferences;

        config.PowerPasteDelimiter = PowerPasteDelimiter;
        config.PowerPasteTrim = PowerPasteTrim;
        config.PowerPasteIncludeDelimiter = PowerPasteIncludeDelimiter;
        config.PowerPasteLoop = PowerPasteLoop;
        config.PowerPasteExplode = PowerPasteExplode;
        OutboundFilter.Save();

        _logger.LogDebug("PowerPaste configuration saved");
    }

    /// <summary>
    /// Resets PowerPaste settings to defaults.
    /// </summary>
    [RelayCommand]
    private void ResetToDefaults()
    {
        PowerPasteDelimiter = ",.;:\\n\\t";
        PowerPasteTrim = true;
        PowerPasteIncludeDelimiter = false;
        PowerPasteLoop = false;
        PowerPasteExplode = false;
        OutboundFilter.ResetToDefaults();

        _logger.LogInformation("PowerPaste settings reset to defaults");
    }
}
