using System.Collections.ObjectModel;
using ClipMate.App.Views.Dialogs;
using ClipMate.Core.Events;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Application = System.Windows.Application;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the QuickPaste options tab.
/// </summary>
public partial class QuickPasteOptionsViewModel : ObservableObject
{
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<QuickPasteOptionsViewModel> _logger;
    private readonly IMessenger _messenger;

    [ObservableProperty]
    private bool _quickPasteAutoTargetingEnabled;

    [ObservableProperty]
    private ObservableCollection<QuickPasteFormattingString> _quickPasteFormattingStrings = [];

    [ObservableProperty]
    private bool _quickPastePasteOnDoubleClick;

    [ObservableProperty]
    private bool _quickPastePasteOnEnter;

    [ObservableProperty]
    private QuickPasteFormattingString? _selectedFormattingString;

    public QuickPasteOptionsViewModel(IConfigurationService configurationService,
        IMessenger messenger,
        ILogger<QuickPasteOptionsViewModel> logger)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        GoodTargets = new EditableStringListViewModel(PromptForTarget);
        BadTargets = new EditableStringListViewModel(PromptForTarget);
        BadTitles = new EditableStringListViewModel(PromptForTitle);
    }

    /// <summary>
    /// Gets the GOOD PROCESSNAME:CLASSNAME list: windows that are always a suitable Paste Target.
    /// </summary>
    public EditableStringListViewModel GoodTargets { get; }

    /// <summary>
    /// Gets the BAD PROCESSNAME:CLASSNAME list: windows that are never the Paste Target.
    /// </summary>
    public EditableStringListViewModel BadTargets { get; }

    /// <summary>
    /// Gets the bad window titles: windows whose title starts with one of them are never the Paste Target.
    /// </summary>
    public EditableStringListViewModel BadTitles { get; }

    /// <summary>
    /// Loads QuickPaste configuration.
    /// </summary>
    public void LoadAsync()
    {
        var config = _configurationService.Configuration.Preferences;

        QuickPasteAutoTargetingEnabled = config.QuickPasteAutoTargetingEnabled;
        QuickPastePasteOnEnter = config.QuickPastePasteOnEnter;
        QuickPastePasteOnDoubleClick = config.QuickPastePasteOnDoubleClick;
        GoodTargets.Load(config.QuickPasteGoodTargets);
        BadTargets.Load(config.QuickPasteBadTargets);
        BadTitles.Load(config.QuickPasteBadTitles);
        QuickPasteFormattingStrings = new ObservableCollection<QuickPasteFormattingString>(config.QuickPasteFormattingStrings);

        _logger.LogDebug("QuickPaste configuration loaded");
    }

    /// <summary>
    /// Saves QuickPaste configuration.
    /// </summary>
    public void SaveAsync()
    {
        var config = _configurationService.Configuration.Preferences;

        config.QuickPasteAutoTargetingEnabled = QuickPasteAutoTargetingEnabled;
        config.QuickPastePasteOnEnter = QuickPastePasteOnEnter;
        config.QuickPastePasteOnDoubleClick = QuickPastePasteOnDoubleClick;
        config.QuickPasteGoodTargets = GoodTargets.ToList();
        config.QuickPasteBadTargets = BadTargets.ToList();
        config.QuickPasteBadTitles = BadTitles.ToList();
        config.QuickPasteFormattingStrings = QuickPasteFormattingStrings.ToList();

        // Broadcast QuickPaste configuration changed event for immediate reload
        _messenger.Send(new QuickPasteConfigurationChangedEvent());

        _logger.LogDebug("QuickPaste configuration saved");
    }

    private static string? PromptForTarget(string current) =>
        ShowListEntryDialog(new QuickPasteTargetDialog(current));

    private static string? PromptForTitle(string current) =>
        ShowListEntryDialog(QuickPasteTargetDialog.ForWindowTitle(current));

    private static string? ShowListEntryDialog(QuickPasteTargetDialog dialog)
    {
        dialog.Owner = Application.Current.GetDialogOwner();
        return dialog.ShowDialog() == true
            ? dialog.EnteredText
            : null;
    }

    /// <summary>
    /// Adds a new formatting string.
    /// </summary>
    [RelayCommand]
    private void AddFormattingString()
    {
        var dialog = new QuickPasteFormattingStringDialog
        {
            Owner = Application.Current.GetDialogOwner(),
        };

        if (dialog.ShowDialog() != true || dialog.FormattingString == null)
            return;

        QuickPasteFormattingStrings.Add(dialog.FormattingString);
        _logger.LogDebug("Added formatting string: {Title}", dialog.FormattingString.Title);
    }

    /// <summary>
    /// Edits the selected formatting string.
    /// </summary>
    [RelayCommand]
    private void EditFormattingString()
    {
        if (SelectedFormattingString == null)
        {
            _logger.LogWarning("No formatting string selected for editing");
            return;
        }

        var index = QuickPasteFormattingStrings.IndexOf(SelectedFormattingString);
        if (index < 0)
        {
            _logger.LogWarning("Selected formatting string not found in collection");
            return;
        }

        var dialog = new QuickPasteFormattingStringDialog(SelectedFormattingString)
        {
            Owner = Application.Current.GetDialogOwner(),
        };

        if (dialog.ShowDialog() != true || dialog.FormattingString == null)
            return;

        QuickPasteFormattingStrings[index] = dialog.FormattingString;
        _logger.LogDebug("Edited formatting string: {Title}", dialog.FormattingString.Title);
    }

    /// <summary>
    /// Deletes the selected formatting string.
    /// </summary>
    [RelayCommand]
    private void DeleteFormattingString()
    {
        if (SelectedFormattingString == null)
        {
            _logger.LogWarning("No formatting string selected for deletion");
            return;
        }

        var format = SelectedFormattingString;
        QuickPasteFormattingStrings.Remove(SelectedFormattingString);
        _logger.LogDebug("Deleted formatting string: {Title}", format.Title);
    }
}
