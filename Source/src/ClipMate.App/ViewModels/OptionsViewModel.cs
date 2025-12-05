using ClipMate.Core.Events;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace ClipMate.App.ViewModels;

/// <summary>
/// Coordinator ViewModel for the Options dialog.
/// Delegates to child ViewModels for each tab.
/// </summary>
public partial class OptionsViewModel : ObservableObject
{
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<OptionsViewModel> _logger;
    private readonly IMessenger _messenger;

    [ObservableProperty]
    private int _selectedTabIndex;

    // Child ViewModels for each tab
    public GeneralOptionsViewModel General { get; }
    public PowerPasteOptionsViewModel PowerPaste { get; }
    public QuickPasteOptionsViewModel QuickPaste { get; }
    public EditorOptionsViewModel Editor { get; }
    public CapturingOptionsViewModel Capturing { get; }
    public ApplicationProfilesOptionsViewModel ApplicationProfiles { get; }
    public SoundsOptionsViewModel Sounds { get; }

    public OptionsViewModel(
        IConfigurationService configurationService,
        IMessenger messenger,
        ILogger<OptionsViewModel> logger,
        GeneralOptionsViewModel generalOptionsViewModel,
        PowerPasteOptionsViewModel powerPasteOptionsViewModel,
        QuickPasteOptionsViewModel quickPasteOptionsViewModel,
        EditorOptionsViewModel editorOptionsViewModel,
        CapturingOptionsViewModel capturingOptionsViewModel,
        ApplicationProfilesOptionsViewModel applicationProfilesOptionsViewModel,
        SoundsOptionsViewModel soundsOptionsViewModel)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        General = generalOptionsViewModel ?? throw new ArgumentNullException(nameof(generalOptionsViewModel));
        PowerPaste = powerPasteOptionsViewModel ?? throw new ArgumentNullException(nameof(powerPasteOptionsViewModel));
        QuickPaste = quickPasteOptionsViewModel ?? throw new ArgumentNullException(nameof(quickPasteOptionsViewModel));
        Editor = editorOptionsViewModel ?? throw new ArgumentNullException(nameof(editorOptionsViewModel));
        Capturing = capturingOptionsViewModel ?? throw new ArgumentNullException(nameof(capturingOptionsViewModel));
        ApplicationProfiles = applicationProfilesOptionsViewModel ?? throw new ArgumentNullException(nameof(applicationProfilesOptionsViewModel));
        Sounds = soundsOptionsViewModel ?? throw new ArgumentNullException(nameof(soundsOptionsViewModel));

        // Note: LoadConfigurationAsync() will be called from the View's Loaded event
    }

    /// <summary>
    /// Selects a tab by name.
    /// </summary>
    /// <param name="tabName">Name of the tab to select (General, Pasting, QuickPaste, etc.)</param>
    public void SelectTab(string? tabName)
    {
        if (string.IsNullOrEmpty(tabName))
            return;

        SelectedTabIndex = tabName.ToUpperInvariant() switch
        {
            "GENERAL" => 0,
            "PASTING" => 1,
            "QUICKPASTE" => 2,
            var _ => SelectedTabIndex,
        };

        _logger.LogDebug("Selected tab: {TabName} (index: {Index})", tabName, SelectedTabIndex);
    }

    /// <summary>
    /// Loads configuration from all child ViewModels.
    /// </summary>
    public async Task LoadConfigurationAsync()
    {
        await General.LoadAsync();
        PowerPaste.LoadAsync();
        QuickPaste.LoadAsync();
        Editor.LoadAsync();
        Capturing.LoadAsync();
        await ApplicationProfiles.LoadAsync();
        Sounds.LoadAsync();

        _logger.LogDebug("Configuration loaded into all child ViewModels");
    }

    /// <summary>
    /// Saves the configuration from all child ViewModels.
    /// </summary>
    [RelayCommand]
    private async Task OkAsync()
    {
        try
        {
            // Save from all child ViewModels
            await General.SaveAsync();
            PowerPaste.SaveAsync();
            QuickPaste.SaveAsync();
            Editor.SaveAsync();
            Capturing.SaveAsync();
            await ApplicationProfiles.SaveAsync();
            Sounds.SaveAsync();

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
}
