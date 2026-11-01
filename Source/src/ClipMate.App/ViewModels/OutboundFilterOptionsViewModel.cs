using ClipMate.App.Views.Dialogs;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using Application = System.Windows.Application;

namespace ClipMate.App.ViewModels;

/// <summary>
/// The Outbound Clip Filtering section of the Pasting options page, as on ClipMate 7's Pasting tab (manual: Pasting
/// Options, "Filter Outbound Clips").
/// </summary>
public partial class OutboundFilterOptionsViewModel : ObservableObject
{
    private readonly IConfigurationService _configurationService;
    private readonly ICurrentClipWriter _currentClipWriter;
    private readonly Func<string, string?> _promptForProgram;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private OutboundFilterMode _mode;

    public OutboundFilterOptionsViewModel(IConfigurationService configurationService, ICurrentClipWriter currentClipWriter)
        : this(configurationService, currentClipWriter, ShowProgramDialog)
    {
    }

    /// <param name="configurationService">The saved settings.</param>
    /// <param name="currentClipWriter">Holds the Current Clip, whose Source Application Add offers.</param>
    /// <param name="promptForProgram">Asks for a program name, starting from the one given; null when canceled.</param>
    internal OutboundFilterOptionsViewModel(IConfigurationService configurationService, ICurrentClipWriter currentClipWriter,
        Func<string, string?> promptForProgram)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _currentClipWriter = currentClipWriter ?? throw new ArgumentNullException(nameof(currentClipWriter));
        _promptForProgram = promptForProgram ?? throw new ArgumentNullException(nameof(promptForProgram));
        Exclusions = new EditableStringListViewModel(PromptForExclusion);
    }

    /// <summary>
    /// The Source Applications whose clips are left whole.
    /// </summary>
    public EditableStringListViewModel Exclusions { get; }

    /// <summary>
    /// Loads the saved settings.
    /// </summary>
    public void Load()
    {
        var preferences = _configurationService.Configuration.Preferences;
        IsEnabled = preferences.OutboundFilter;
        Mode = preferences.OutboundFilterMode;
        Exclusions.Load(preferences.OutboundFilterExclusions);
    }

    /// <summary>
    /// Saves the settings.
    /// </summary>
    public void Save()
    {
        var preferences = _configurationService.Configuration.Preferences;
        preferences.OutboundFilter = IsEnabled;
        preferences.OutboundFilterMode = Mode;
        preferences.OutboundFilterExclusions = Exclusions.ToList();
    }

    /// <summary>
    /// ClipMate 7's defaults: off, Passive, Explorer excluded.
    /// </summary>
    public void ResetToDefaults()
    {
        var defaults = new PreferencesConfiguration();
        IsEnabled = defaults.OutboundFilter;
        Mode = defaults.OutboundFilterMode;
        Exclusions.Load(defaults.OutboundFilterExclusions);
    }

    // Adding starts from the Current Clip's Source Application, as in ClipMate 7
    private string? PromptForExclusion(string current) =>
        _promptForProgram(current.Length > 0
            ? current
            : _currentClipWriter.CurrentClip?.Clip.SourceApplicationName ?? string.Empty);

    private static string? ShowProgramDialog(string current)
    {
        var dialog = QuickPasteTargetDialog.ForProgram(current);
        dialog.Owner = Application.Current.GetDialogOwner();

        return dialog.ShowDialog() == true
            ? dialog.EnteredText
            : null;
    }
}
