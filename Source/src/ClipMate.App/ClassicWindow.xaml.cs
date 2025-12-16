using System.ComponentModel;
using System.Diagnostics;
using ClipMate.App.ViewModels;
using ClipMate.Core.Services;

namespace ClipMate.App;

/// <summary>
/// ClipMate Classic window with complete menu, toolbar, and clip list.
/// Features stay-on-top and TOML configuration persistence for window state.
/// </summary>
public partial class ClassicWindow
{
    private readonly IConfigurationService _configurationService;
    private readonly bool _isHotkeyTriggered;
    private readonly IQuickPasteService _quickPasteService;
    private readonly ClassicViewModel _viewModel;

    public ClassicWindow(ClassicViewModel viewModel, IConfigurationService configurationService, IQuickPasteService quickPasteService, bool isHotkeyTriggered = false)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _quickPasteService = quickPasteService ?? throw new ArgumentNullException(nameof(quickPasteService));
        _isHotkeyTriggered = isHotkeyTriggered;
        DataContext = _viewModel;

        // Subscribe to close window flag
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        // Subscribe to window deactivation for QuickPaste target updates
        Deactivated += ClassicWindow_Deactivated;
        Activated += ClassicWindow_Activated;

        // Load configuration values
        Topmost = _configurationService.Configuration.Preferences.ClassicStayOnTop;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ClassicViewModel.ShouldCloseWindow) && _viewModel.ShouldCloseWindow)
            Close();
    }

    private void ClassicWindow_Activated(object? sender, EventArgs e)
    {
        // When main window is activated, bring any owned modal dialogs to front
        // This fixes the issue where dialogs can get hidden when switching between apps
        foreach (Window ownedWindow in OwnedWindows)
        {
            if (!ownedWindow.IsVisible || ownedWindow.IsActive)
                continue;

            ownedWindow.Activate();
            ownedWindow.Topmost = true;
            ownedWindow.Topmost = false; // Flash to bring to front
            break; // Only activate the top-most owned dialog
        }
    }

    private async void ClassicWindow_Deactivated(object? sender, EventArgs e)
    {
        try
        {
            // Delay to ensure the new foreground window is fully activated
            await Task.Delay(100);
            _quickPasteService.UpdateTarget();
        }
        catch (Exception ex)
        {
            // Log error silently - QuickPaste target update failures should not crash the window
            Debug.WriteLine($"Error updating QuickPaste target on window deactivation: {ex.Message}");
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var config = _configurationService.Configuration.Preferences;

        // Load saved window state based on invocation mode
        var windowState = _isHotkeyTriggered
            ? config.ClassicWindowHotkey
            : config.ClassicWindowTaskbar;

        // Restore window position and size if saved
        if (windowState.Left.HasValue)
            Left = windowState.Left.Value;

        if (windowState.Top.HasValue)
            Top = windowState.Top.Value;

        if (windowState.Width.HasValue)
            Width = windowState.Width.Value;

        if (windowState.Height.HasValue)
            Height = windowState.Height.Value;

        // Load tack state
        _viewModel.IsTacked = config.ClassicIsTacked;
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        Activated -= ClassicWindow_Activated;
        Deactivated -= ClassicWindow_Deactivated;

        var config = _configurationService.Configuration.Preferences;

        // Save window state based on invocation mode
        var windowState = _isHotkeyTriggered
            ? config.ClassicWindowHotkey
            : config.ClassicWindowTaskbar;

        // Save current window position and size
        windowState.Left = Left;
        windowState.Top = Top;
        windowState.Width = ActualWidth;
        windowState.Height = ActualHeight;

        // Save tack state
        config.ClassicIsTacked = _viewModel.IsTacked;

        _ = _configurationService.SaveAsync();
    }
}
