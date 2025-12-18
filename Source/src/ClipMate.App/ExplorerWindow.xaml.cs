using System.ComponentModel;
using System.Windows.Input;
using ClipMate.App.Services;
using ClipMate.App.ViewModels;
using ClipMate.App.Views;
using ClipMate.Core.Events;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ModifierKeys = System.Windows.Input.ModifierKeys;

namespace ClipMate.App;

/// <summary>
/// Interaction logic for ExplorerWindow.xaml
/// Responsible only for window chrome (tray icon, window state, etc.)
/// All business logic is in ExplorerWindowViewModel.
/// </summary>
public partial class ExplorerWindow : IWindow,
    IRecipient<ShowExplorerWindowEvent>,
    IRecipient<ShowTaskbarIconChangedEvent>
{
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<ExplorerWindow>? _logger;
    private readonly IMessenger _messenger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ExplorerWindowViewModel _viewModel;
    private bool _isExiting;

    public ExplorerWindow(ExplorerWindowViewModel explorerWindowViewModel,
        IServiceProvider serviceProvider,
        IMessenger messenger,
        IConfigurationService configurationService,
        ILogger<ExplorerWindow>? logger = null)
    {
        InitializeComponent();

        _viewModel = explorerWindowViewModel ?? throw new ArgumentNullException(nameof(explorerWindowViewModel));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _logger = logger;

        DataContext = _viewModel;

        // Apply ShowTaskbarIcon configuration
        ShowInTaskbar = _configurationService.Configuration.Preferences.ShowTaskbarIcon;

        // Register for hotkey events and configuration changes
        _messenger.Register<ShowExplorerWindowEvent>(this);
        _messenger.Register<ShowTaskbarIconChangedEvent>(this);

        Loaded += ExplorerWindow_Loaded;
        Closing += ExplorerWindow_Closing;
        PreviewKeyDown += ExplorerWindow_PreviewKeyDown;
        Activated += ExplorerWindow_Activated;
        Deactivated += ExplorerWindow_Deactivated;

        // Subscribe to events
        _messenger.Register<OpenOptionsDialogEvent>(this, (_, message) => ShowOptions(message.TabName));
        _messenger.Register<OpenTextToolsDialogEvent>(this, (_, _) => ShowTextTools());
        _messenger.Register<ExitApplicationEvent>(this, (_, _) => ExitApplication());
    }

    /// <summary>
    /// Handles ShowExplorerWindowEvent from hotkey.
    /// </summary>
    public void Receive(ShowExplorerWindowEvent message)
    {
        // Ensure we're on the UI thread
        Dispatcher.InvokeAsync(() =>
        {
            Show();
            Activate();
            WindowState = WindowState.Normal;
            _logger?.LogDebug("ExplorerWindow shown from hotkey");
        });
    }

    /// <summary>
    /// Handles ShowTaskbarIconChangedEvent from configuration changes.
    /// </summary>
    public void Receive(ShowTaskbarIconChangedEvent message)
    {
        Dispatcher.InvokeAsync(() =>
        {
            ShowInTaskbar = message.ShowTaskbarIcon;
            _logger?.LogDebug("ExplorerWindow ShowInTaskbar changed to {ShowInTaskbar}", message.ShowTaskbarIcon);
        });
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        // Hide window and taskbar icon when minimized (will remain visible in system tray)
        if (WindowState == WindowState.Minimized)
        {
            Hide();
            ShowInTaskbar = false;
            _logger?.LogDebug("ExplorerWindow minimized to tray");
        }
        else if (WindowState is WindowState.Normal or WindowState.Maximized)
            ShowInTaskbar = true;
    }

    private void ShowMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Show();
        Activate();
        WindowState = WindowState.Normal;
        _logger?.LogDebug("ExplorerWindow shown from tray menu");
    }

    /// <summary>
    /// Prepares the window for application exit (skips minimize to tray behavior)
    /// </summary>
    public void PrepareForExit() => _isExiting = true;

    private void ExplorerWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.T || (Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
            return;

        ShowTextTools();
        e.Handled = true;
    }

    private void ExplorerWindow_Deactivated(object? sender, EventArgs e)
    {
        // Capture target window when ClipMate loses focus
        _viewModel.OnWindowDeactivated();
    }

    private void ExplorerWindow_Activated(object? sender, EventArgs e)
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

    private void TextTools_Click(object sender, RoutedEventArgs e) => ShowTextTools();

    private void ShowTextTools()
    {
        try
        {
            if (_serviceProvider.GetService(typeof(TextToolsDialog)) is not TextToolsDialog textToolsDialog)
                return;

            textToolsDialog.Owner = this;
            textToolsDialog.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to show Text Tools dialog");
            MessageBox.Show($"Failed to open Text Tools: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Options_Click(object sender, RoutedEventArgs e) => ShowOptions();

    private void ShowOptions(string? selectedTab = null)
    {
        try
        {
            if (_serviceProvider.GetService(typeof(OptionsDialog)) is not OptionsDialog optionsDialog)
                return;

            // Set the selected tab if specified
            if (!string.IsNullOrEmpty(selectedTab) && optionsDialog.DataContext is OptionsViewModel viewModel)
                viewModel.SelectTab(selectedTab);

            optionsDialog.Owner = this;
            var result = optionsDialog.ShowDialog();

            if (result == true)
                _logger?.LogInformation("Options saved successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to show Options dialog");
            MessageBox.Show($"Failed to open Options: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ExplorerWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Initialize ExplorerWindowViewModel (loads all child VMs, data, etc.)
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize ExplorerWindow");
        }
    }

    /// <summary>
    /// Handles File → Exit menu click
    /// </summary>
    private void Exit_Click(object sender, RoutedEventArgs e) => ExitApplication();

    /// <summary>
    /// Exits the application (called from menu or event).
    /// </summary>
    private void ExitApplication()
    {
        _logger?.LogInformation("Exit - shutting down application");
        _isExiting = true;
        Application.Current.Shutdown();
    }

    /// <summary>
    /// Handles the Closing event to minimize to tray instead of exiting.
    /// Hold Shift while closing to force exit.
    /// </summary>
    private void ExplorerWindow_Closing(object? sender, CancelEventArgs e)
    {
        // If already exiting (from File→Exit or tray menu), allow it
        if (_isExiting)
        {
            _logger?.LogInformation("ExplorerWindow closing - application is exiting");
            return;
        }

        // Check if Shift key is held - if so, allow actual exit
        if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            _logger?.LogInformation("ExplorerWindow closing with Shift key - allowing exit");
            _isExiting = true;
            return;
        }

        // Cancel the close and hide the window instead
        e.Cancel = true;
        Hide();
        _logger?.LogInformation("ExplorerWindow minimized to system tray");
    }

    private void SecondaryClipListView_SelectionChanged(object sender, RoutedEventArgs e)
    {
        // Selection changes in secondary list (if dual mode is implemented)
        _logger?.LogDebug("Secondary ClipList selection changed");
    }
}
