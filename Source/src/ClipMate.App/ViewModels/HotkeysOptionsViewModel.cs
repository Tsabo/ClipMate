using ClipMate.App.Helpers;
using ClipMate.App.Services;
using ClipMate.App.Views;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Application = System.Windows.Application;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for managing hotkey configuration.
/// </summary>
public partial class HotkeysOptionsViewModel : ObservableObject
{
    private readonly IConfigurationService _configurationService;
    private readonly HotkeyCoordinator _hotkeyCoordinator;
    private readonly IHotkeyService _hotkeyService;

    [ObservableProperty]
    private string _activateQuickPaste = "Shift+Ctrl+Q";

    [ObservableProperty]
    private string _manualCapture = "Ctrl+Shift+C";

    [ObservableProperty]
    private string _manualFilter = "Ctrl+Alt+W";

    [ObservableProperty]
    private string _objectScreenCapture = "Ctrl+Alt+F11";

    [ObservableProperty]
    private string _popupClipBar = "Ctrl+Shift+Alt+C";

    [ObservableProperty]
    private string _regionScreenCapture = "Ctrl+Alt+F12";

    [ObservableProperty]
    private string _scrollNext = "Ctrl+Alt+N";

    [ObservableProperty]
    private string _scrollPrevious = "Ctrl+Alt+P";

    [ObservableProperty]
    private string _showWindow = "Ctrl+Alt+C";

    [ObservableProperty]
    private string? _testResult;

    [ObservableProperty]
    private bool _testResultIsSuccess;

    [ObservableProperty]
    private string _toggleAutoCapture = "Ctrl+Alt+A";

    [ObservableProperty]
    private string _viewClipInFloatingWindow = "Ctrl+Alt+F2";

    public HotkeysOptionsViewModel(IConfigurationService configurationService,
        IHotkeyService hotkeyService,
        HotkeyCoordinator hotkeyCoordinator)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _hotkeyService = hotkeyService ?? throw new ArgumentNullException(nameof(hotkeyService));
        _hotkeyCoordinator = hotkeyCoordinator ?? throw new ArgumentNullException(nameof(hotkeyCoordinator));
    }

    /// <summary>
    /// Loads the hotkey configuration from the configuration service.
    /// </summary>
    public Task LoadConfigurationAsync()
    {
        var config = _configurationService.Configuration.Hotkeys;

        ShowWindow = config.Activate;
        ScrollNext = config.SelectNext;
        ScrollPrevious = config.SelectPrevious;
        ActivateQuickPaste = config.QuickPaste;
        RegionScreenCapture = config.ScreenCapture;
        ObjectScreenCapture = config.ScreenCaptureObject;
        ViewClipInFloatingWindow = config.ViewClip;
        PopupClipBar = config.PopupClipBar;
        ToggleAutoCapture = config.AutoCapture;
        ManualCapture = config.Capture;
        ManualFilter = config.ManualFilter;

        TestResult = null;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Saves the hotkey configuration to the configuration service.
    /// </summary>
    public Task SaveConfigurationAsync()
    {
        var config = _configurationService.Configuration.Hotkeys;

        config.Activate = ShowWindow;
        config.SelectNext = ScrollNext;
        config.SelectPrevious = ScrollPrevious;
        config.QuickPaste = ActivateQuickPaste;
        config.ScreenCapture = RegionScreenCapture;
        config.ScreenCaptureObject = ObjectScreenCapture;
        config.ViewClip = ViewClipInFloatingWindow;
        config.PopupClipBar = PopupClipBar;
        config.AutoCapture = ToggleAutoCapture;
        config.Capture = ManualCapture;
        config.ManualFilter = ManualFilter;

        // Reload hotkeys to apply the new configuration
        _hotkeyCoordinator.ReloadHotkeys();

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task TestHotkeyAsync(string? hotkeyProperty)
    {
        if (string.IsNullOrEmpty(hotkeyProperty))
            return;

        // Get the hotkey string and corresponding hotkey ID for the property
        var (hotkeyString, hotkeyId) = hotkeyProperty switch
        {
            nameof(ShowWindow) => (ShowWindow, 1),
            nameof(ScrollNext) => (ScrollNext, 2),
            nameof(ScrollPrevious) => (ScrollPrevious, 3),
            nameof(ActivateQuickPaste) => (ActivateQuickPaste, 4),
            nameof(RegionScreenCapture) => (RegionScreenCapture, 5),
            nameof(ObjectScreenCapture) => (ObjectScreenCapture, 6),
            nameof(ViewClipInFloatingWindow) => (ViewClipInFloatingWindow, 7),
            nameof(PopupClipBar) => (PopupClipBar, 8),
            nameof(ToggleAutoCapture) => (ToggleAutoCapture, 9),
            nameof(ManualCapture) => (ManualCapture, 10),
            nameof(ManualFilter) => (ManualFilter, 11),
            var _ => (null, 0),
        };

        if (string.IsNullOrWhiteSpace(hotkeyString))
        {
            TestResult = "No hotkey specified";
            TestResultIsSuccess = false;
            return;
        }

        // Parse the hotkey
        if (!HotkeyParser.TryParse(hotkeyString, out var modifiers, out var virtualKey, out var errorMessage))
        {
            TestResult = $"✗ Invalid: {errorMessage}";
            TestResultIsSuccess = false;
            return;
        }

        // Use a temporary ID for testing
        var testId = 9999;
        var registered = false;
        var wasRegistered = _hotkeyService.IsHotkeyRegistered(hotkeyId);

        try
        {
            TestResult = "Testing...";
            TestResultIsSuccess = false;

            // Temporarily unregister the current hotkey for this property
            // so we can test if the new combination is available
            if (wasRegistered)
                _hotkeyService.UnregisterHotkey(hotkeyId);

            await Task.Delay(100); // Brief delay for visual feedback

            // Attempt registration with test ID
            registered = _hotkeyService.RegisterHotkey(testId, modifiers, virtualKey, () => { });

            if (registered)
            {
                TestResult = "✓ Hotkey is available";
                TestResultIsSuccess = true;
            }
            else
            {
                TestResult = $"✗ Cannot register '{hotkeyString}' - already in use by another application";
                TestResultIsSuccess = false;
            }
        }
        finally
        {
            // Always unregister test hotkey if we registered it
            if (registered)
                _hotkeyService.UnregisterHotkey(testId);

            // Re-register the original hotkey if it was registered before
            if (wasRegistered)
                _hotkeyCoordinator.ReloadHotkeys();
        }
    }

    [RelayCommand]
    private void BindHotkey(string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
            return;

        var dialog = new HotkeyBindDialog
        {
            Owner = Application.Current.MainWindow,
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.CapturedHotkey))
        {
            // Update the appropriate property based on the parameter
            switch (propertyName)
            {
                case nameof(ShowWindow):
                    ShowWindow = dialog.CapturedHotkey;
                    break;
                case nameof(ScrollNext):
                    ScrollNext = dialog.CapturedHotkey;
                    break;
                case nameof(ScrollPrevious):
                    ScrollPrevious = dialog.CapturedHotkey;
                    break;
                case nameof(ActivateQuickPaste):
                    ActivateQuickPaste = dialog.CapturedHotkey;
                    break;
                case nameof(RegionScreenCapture):
                    RegionScreenCapture = dialog.CapturedHotkey;
                    break;
                case nameof(ObjectScreenCapture):
                    ObjectScreenCapture = dialog.CapturedHotkey;
                    break;
                case nameof(ViewClipInFloatingWindow):
                    ViewClipInFloatingWindow = dialog.CapturedHotkey;
                    break;
                case nameof(PopupClipBar):
                    PopupClipBar = dialog.CapturedHotkey;
                    break;
                case nameof(ToggleAutoCapture):
                    ToggleAutoCapture = dialog.CapturedHotkey;
                    break;
                case nameof(ManualCapture):
                    ManualCapture = dialog.CapturedHotkey;
                    break;
                case nameof(ManualFilter):
                    ManualFilter = dialog.CapturedHotkey;
                    break;
            }

            TestResult = $"Hotkey bound to '{dialog.CapturedHotkey}'";
            TestResultIsSuccess = true;
        }
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        ShowWindow = "Ctrl+Alt+C";
        ScrollNext = "Ctrl+Alt+N";
        ScrollPrevious = "Ctrl+Alt+P";
        ActivateQuickPaste = "Shift+Ctrl+Q";
        RegionScreenCapture = "Ctrl+Alt+F12";
        ObjectScreenCapture = "Ctrl+Alt+F11";
        ViewClipInFloatingWindow = "Ctrl+Alt+F2";
        PopupClipBar = "Ctrl+Shift+Alt+C";
        ToggleAutoCapture = "Ctrl+Alt+A";
        ManualCapture = "Ctrl+Shift+C";
        ManualFilter = "Ctrl+Alt+W";

        TestResult = "Hotkeys reset to default values";
        TestResultIsSuccess = true;
    }
}
