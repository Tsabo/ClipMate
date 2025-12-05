using System.Collections.ObjectModel;
using ClipMate.Core.Events;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

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
    private ObservableCollection<string> _quickPasteBadTargets = [];

    [ObservableProperty]
    private ObservableCollection<QuickPasteFormattingString> _quickPasteFormattingStrings = [];

    [ObservableProperty]
    private ObservableCollection<string> _quickPasteGoodTargets = [];

    [ObservableProperty]
    private bool _quickPastePasteOnDoubleClick;

    [ObservableProperty]
    private bool _quickPastePasteOnEnter;

    [ObservableProperty]
    private bool _quickPasteUseMonitoringThread;

    [ObservableProperty]
    private int _selectedBadTargetIndex = -1;

    [ObservableProperty]
    private QuickPasteFormattingString? _selectedFormattingString;

    [ObservableProperty]
    private int _selectedGoodTargetIndex = -1;

    public QuickPasteOptionsViewModel(
        IConfigurationService configurationService,
        IMessenger messenger,
        ILogger<QuickPasteOptionsViewModel> logger)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Loads QuickPaste configuration.
    /// </summary>
    public void LoadAsync()
    {
        var config = _configurationService.Configuration.Preferences;

        QuickPasteAutoTargetingEnabled = config.QuickPasteAutoTargetingEnabled;
        QuickPasteUseMonitoringThread = config.QuickPasteUseMonitoringThread;
        QuickPastePasteOnEnter = config.QuickPastePasteOnEnter;
        QuickPastePasteOnDoubleClick = config.QuickPastePasteOnDoubleClick;
        QuickPasteGoodTargets = new ObservableCollection<string>(config.QuickPasteGoodTargets);
        QuickPasteBadTargets = new ObservableCollection<string>(config.QuickPasteBadTargets);
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
        config.QuickPasteGoodTargets = QuickPasteGoodTargets.ToList();
        config.QuickPasteBadTargets = QuickPasteBadTargets.ToList();
        config.QuickPasteFormattingStrings = QuickPasteFormattingStrings.ToList();

        // Broadcast QuickPaste configuration changed event for immediate reload
        _messenger.Send(new QuickPasteConfigurationChangedEvent());

        _logger.LogDebug("QuickPaste configuration saved");
    }

    /// <summary>
    /// Adds a new GOOD target specification.
    /// </summary>
    [RelayCommand]
    private void AddGoodTarget()
    {
        var dialog = new Views.QuickPasteTargetDialog();
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
        if (SelectedGoodTargetIndex < 0 || SelectedGoodTargetIndex >= QuickPasteGoodTargets.Count)
        {
            _logger.LogWarning("No GOOD target selected for editing");
            return;
        }

        var currentTarget = QuickPasteGoodTargets[SelectedGoodTargetIndex];
        var dialog = new Views.QuickPasteTargetDialog(currentTarget);
        if (dialog.ShowDialog() == true)
        {
            var target = dialog.TargetSpecification;
            if (!string.IsNullOrWhiteSpace(target))
            {
                QuickPasteGoodTargets[SelectedGoodTargetIndex] = target;
                _logger.LogDebug("Edited GOOD target from {Old} to {New}", currentTarget, target);
            }
        }
    }

    /// <summary>
    /// Deletes the selected GOOD target specification.
    /// </summary>
    [RelayCommand]
    private void DeleteGoodTarget()
    {
        if (SelectedGoodTargetIndex < 0 || SelectedGoodTargetIndex >= QuickPasteGoodTargets.Count)
        {
            _logger.LogWarning("No GOOD target selected for deletion");
            return;
        }

        var target = QuickPasteGoodTargets[SelectedGoodTargetIndex];
        QuickPasteGoodTargets.RemoveAt(SelectedGoodTargetIndex);
        _logger.LogDebug("Deleted GOOD target: {Target}", target);
    }

    /// <summary>
    /// Adds a new BAD target specification.
    /// </summary>
    [RelayCommand]
    private void AddBadTarget()
    {
        var dialog = new Views.QuickPasteTargetDialog();
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
        if (SelectedBadTargetIndex < 0 || SelectedBadTargetIndex >= QuickPasteBadTargets.Count)
        {
            _logger.LogWarning("No BAD target selected for editing");
            return;
        }

        var currentTarget = QuickPasteBadTargets[SelectedBadTargetIndex];
        var dialog = new Views.QuickPasteTargetDialog(currentTarget);
        if (dialog.ShowDialog() == true)
        {
            var target = dialog.TargetSpecification;
            if (!string.IsNullOrWhiteSpace(target))
            {
                QuickPasteBadTargets[SelectedBadTargetIndex] = target;
                _logger.LogDebug("Edited BAD target from {Old} to {New}", currentTarget, target);
            }
        }
    }

    /// <summary>
    /// Deletes the selected BAD target specification.
    /// </summary>
    [RelayCommand]
    private void DeleteBadTarget()
    {
        if (SelectedBadTargetIndex < 0 || SelectedBadTargetIndex >= QuickPasteBadTargets.Count)
        {
            _logger.LogWarning("No BAD target selected for deletion");
            return;
        }

        var target = QuickPasteBadTargets[SelectedBadTargetIndex];
        QuickPasteBadTargets.RemoveAt(SelectedBadTargetIndex);
        _logger.LogDebug("Deleted BAD target: {Target}", target);
    }

    /// <summary>
    /// Adds a new formatting string.
    /// </summary>
    [RelayCommand]
    private void AddFormattingString()
    {
        var dialog = new Views.QuickPasteFormattingStringDialog();
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

        var dialog = new Views.QuickPasteFormattingStringDialog(SelectedFormattingString);
        if (dialog.ShowDialog() == true && dialog.FormattingString != null)
        {
            QuickPasteFormattingStrings[index] = dialog.FormattingString;
            _logger.LogDebug("Edited formatting string: {Title}", dialog.FormattingString.Title);
        }
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
