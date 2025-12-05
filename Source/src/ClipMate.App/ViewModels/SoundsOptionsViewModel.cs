using System.IO;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the Sounds options tab.
/// </summary>
public partial class SoundsOptionsViewModel : ObservableObject
{
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<SoundsOptionsViewModel> _logger;
    private readonly ISoundService _soundService;

    [ObservableProperty]
    private SoundMode _appendSoundMode = SoundMode.Default;

    [ObservableProperty]
    private SoundMode _clipboardUpdateSoundMode = SoundMode.Default;

    [ObservableProperty]
    private string? _customAppend;

    [ObservableProperty]
    private string? _customClipboardUpdate;

    [ObservableProperty]
    private string? _customErase;

    [ObservableProperty]
    private string? _customFilter;

    [ObservableProperty]
    private string? _customIgnore;

    [ObservableProperty]
    private string? _customPowerPasteComplete;

    [ObservableProperty]
    private SoundMode _eraseSoundMode = SoundMode.Default;

    [ObservableProperty]
    private SoundMode _filterSoundMode = SoundMode.Default;

    [ObservableProperty]
    private SoundMode _ignoreSoundMode = SoundMode.Default;

    [ObservableProperty]
    private SoundMode _powerPasteCompleteSoundMode = SoundMode.Default;

    public SoundsOptionsViewModel(
        IConfigurationService configurationService,
        ISoundService soundService,
        ILogger<SoundsOptionsViewModel> logger)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _soundService = soundService ?? throw new ArgumentNullException(nameof(soundService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Register for property changes to notify UI of dependent properties
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ClipboardUpdateSoundMode))
                OnPropertyChanged(nameof(ClipboardUpdateIsCustom));
            else if (e.PropertyName == nameof(AppendSoundMode))
                OnPropertyChanged(nameof(AppendIsCustom));
            else if (e.PropertyName == nameof(EraseSoundMode))
                OnPropertyChanged(nameof(EraseIsCustom));
            else if (e.PropertyName == nameof(IgnoreSoundMode))
                OnPropertyChanged(nameof(IgnoreIsCustom));
            else if (e.PropertyName == nameof(FilterSoundMode))
                OnPropertyChanged(nameof(FilterIsCustom));
            else if (e.PropertyName == nameof(PowerPasteCompleteSoundMode))
                OnPropertyChanged(nameof(PowerPasteCompleteIsCustom));
        };
    }

    // Helper properties for IsEnabled bindings
    public bool ClipboardUpdateIsCustom => ClipboardUpdateSoundMode == SoundMode.Custom;
    public bool AppendIsCustom => AppendSoundMode == SoundMode.Custom;
    public bool EraseIsCustom => EraseSoundMode == SoundMode.Custom;
    public bool IgnoreIsCustom => IgnoreSoundMode == SoundMode.Custom;
    public bool FilterIsCustom => FilterSoundMode == SoundMode.Custom;
    public bool PowerPasteCompleteIsCustom => PowerPasteCompleteSoundMode == SoundMode.Custom;

    /// <summary>
    /// Loads sound configuration from the configuration service.
    /// </summary>
    public void LoadAsync()
    {
        var soundConfig = _configurationService.Configuration.Preferences.Sound;

        ClipboardUpdateSoundMode = soundConfig.ClipboardUpdate;
        CustomClipboardUpdate = soundConfig.CustomClipboardUpdate;
        AppendSoundMode = soundConfig.Append;
        CustomAppend = soundConfig.CustomAppend;
        EraseSoundMode = soundConfig.Erase;
        CustomErase = soundConfig.CustomErase;
        IgnoreSoundMode = soundConfig.Ignore;
        CustomIgnore = soundConfig.CustomIgnore;
        FilterSoundMode = soundConfig.Filter;
        CustomFilter = soundConfig.CustomFilter;
        PowerPasteCompleteSoundMode = soundConfig.PowerPasteComplete;
        CustomPowerPasteComplete = soundConfig.CustomPowerPasteComplete;

        _logger.LogDebug("Sound configuration loaded into SoundsOptionsViewModel");
    }

    /// <summary>
    /// Saves sound configuration to the configuration service.
    /// </summary>
    public void SaveAsync()
    {
        var soundConfig = _configurationService.Configuration.Preferences.Sound;

        soundConfig.ClipboardUpdate = ClipboardUpdateSoundMode;
        soundConfig.CustomClipboardUpdate = CustomClipboardUpdate;
        soundConfig.Append = AppendSoundMode;
        soundConfig.CustomAppend = CustomAppend;
        soundConfig.Erase = EraseSoundMode;
        soundConfig.CustomErase = CustomErase;
        soundConfig.Ignore = IgnoreSoundMode;
        soundConfig.CustomIgnore = CustomIgnore;
        soundConfig.Filter = FilterSoundMode;
        soundConfig.CustomFilter = CustomFilter;
        soundConfig.PowerPasteComplete = PowerPasteCompleteSoundMode;
        soundConfig.CustomPowerPasteComplete = CustomPowerPasteComplete;

        _logger.LogDebug("Sound configuration saved from SoundsOptionsViewModel");
    }

    [RelayCommand(CanExecute = nameof(CanTestClipboardUpdateSound))]
    private async Task TestClipboardUpdateSoundAsync() => await _soundService.PlaySoundAsync(SoundEvent.ClipboardUpdate);

    private bool CanTestClipboardUpdateSound() =>
        ClipboardUpdateSoundMode != SoundMode.Off &&
        (ClipboardUpdateSoundMode != SoundMode.Custom || !string.IsNullOrWhiteSpace(CustomClipboardUpdate) && File.Exists(CustomClipboardUpdate));

    [RelayCommand(CanExecute = nameof(CanTestAppendSound))]
    private async Task TestAppendSoundAsync() => await _soundService.PlaySoundAsync(SoundEvent.Append);

    private bool CanTestAppendSound() =>
        AppendSoundMode != SoundMode.Off &&
        (AppendSoundMode != SoundMode.Custom || !string.IsNullOrWhiteSpace(CustomAppend) && File.Exists(CustomAppend));

    [RelayCommand(CanExecute = nameof(CanTestEraseSound))]
    private async Task TestEraseSoundAsync() => await _soundService.PlaySoundAsync(SoundEvent.Erase);

    private bool CanTestEraseSound() =>
        EraseSoundMode != SoundMode.Off &&
        (EraseSoundMode != SoundMode.Custom || !string.IsNullOrWhiteSpace(CustomErase) && File.Exists(CustomErase));

    [RelayCommand(CanExecute = nameof(CanTestIgnoreSound))]
    private async Task TestIgnoreSoundAsync() => await _soundService.PlaySoundAsync(SoundEvent.Ignore);

    private bool CanTestIgnoreSound() =>
        IgnoreSoundMode != SoundMode.Off &&
        (IgnoreSoundMode != SoundMode.Custom || !string.IsNullOrWhiteSpace(CustomIgnore) && File.Exists(CustomIgnore));

    [RelayCommand(CanExecute = nameof(CanTestFilterSound))]
    private async Task TestFilterSoundAsync() => await _soundService.PlaySoundAsync(SoundEvent.Filter);

    private bool CanTestFilterSound() =>
        FilterSoundMode != SoundMode.Off &&
        (FilterSoundMode != SoundMode.Custom || !string.IsNullOrWhiteSpace(CustomFilter) && File.Exists(CustomFilter));

    [RelayCommand(CanExecute = nameof(CanTestPowerPasteCompleteSound))]
    private async Task TestPowerPasteCompleteSoundAsync() => await _soundService.PlaySoundAsync(SoundEvent.PowerPasteComplete);

    private bool CanTestPowerPasteCompleteSound() =>
        PowerPasteCompleteSoundMode != SoundMode.Off &&
        (PowerPasteCompleteSoundMode != SoundMode.Custom || !string.IsNullOrWhiteSpace(CustomPowerPasteComplete) && File.Exists(CustomPowerPasteComplete));

    [RelayCommand]
    private void BrowseCustomSound(string soundType)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Sound File",
            Filter = "WAV Files (*.wav)|*.wav|All Files (*.*)|*.*",
            FilterIndex = 1,
            CheckFileExists = true,
        };

        if (dialog.ShowDialog() == true)
        {
            switch (soundType)
            {
                case "ClipboardUpdate":
                    CustomClipboardUpdate = dialog.FileName;
                    break;
                case "Append":
                    CustomAppend = dialog.FileName;
                    break;
                case "Erase":
                    CustomErase = dialog.FileName;
                    break;
                case "Ignore":
                    CustomIgnore = dialog.FileName;
                    break;
                case "Filter":
                    CustomFilter = dialog.FileName;
                    break;
                case "PowerPasteComplete":
                    CustomPowerPasteComplete = dialog.FileName;
                    break;
            }

            _logger.LogDebug("Selected custom sound file for {SoundType}: {FilePath}", soundType, dialog.FileName);
        }
    }
}
