using ClipMate.App.Helpers;
using ClipMate.App.Services;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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
    private string _manualCapture = "Win+C";

    [ObservableProperty]
    private string _manualFilter = "Win+W";

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
    private string _toggleAutoCapture = "Win+Shift+C";

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

        // Get the hotkey string from the property
        var hotkeyString = hotkeyProperty switch
        {
            nameof(ShowWindow) => ShowWindow,
            nameof(ScrollNext) => ScrollNext,
            nameof(ScrollPrevious) => ScrollPrevious,
            nameof(ActivateQuickPaste) => ActivateQuickPaste,
            nameof(RegionScreenCapture) => RegionScreenCapture,
            nameof(ObjectScreenCapture) => ObjectScreenCapture,
            nameof(ViewClipInFloatingWindow) => ViewClipInFloatingWindow,
            nameof(PopupClipBar) => PopupClipBar,
            nameof(ToggleAutoCapture) => ToggleAutoCapture,
            nameof(ManualCapture) => ManualCapture,
            nameof(ManualFilter) => ManualFilter,
            var _ => null,
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

        // Try to register the hotkey with a temporary ID
        var testId = 9999;
        var registered = false;

        try
        {
            TestResult = "Testing...";
            TestResultIsSuccess = false;

            await Task.Delay(100); // Brief delay for visual feedback

            // Attempt registration
            registered = _hotkeyService.RegisterHotkey(testId, modifiers, virtualKey, () => { });

            if (registered)
            {
                TestResult = "✓ Hotkey is available";
                TestResultIsSuccess = true;
            }
            else
            {
                TestResult = $"✗ Cannot register '{hotkeyString}' - already in use";
                TestResultIsSuccess = false;
            }
        }
        finally
        {
            // Always unregister if we successfully registered
            if (registered)
                _hotkeyService.UnregisterHotkey(testId);
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
        ToggleAutoCapture = "Win+Shift+C";
        ManualCapture = "Win+C";
        ManualFilter = "Win+W";

        TestResult = "Hotkeys reset to default values";
        TestResultIsSuccess = true;
    }
}
