using System.ComponentModel;
using System.Windows.Input;
using ClipMate.App.Services;
using ClipMate.App.ViewModels;
using ClipMate.App.Views;
using ClipMate.Core.Events;
using ClipMate.Core.Services;
using ClipMate.Data.Services;
using CommunityToolkit.Mvvm.Messaging;
using DevExpress.Xpf.Bars;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        foreach (Window item in OwnedWindows)
        {
            if (!item.IsVisible || item.IsActive)
                continue;

            item.Activate();
            item.Topmost = true;
            item.Topmost = false; // Flash to bring to front
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

            // Load collection dropdowns
            //await LoadCollectionDropdownsAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize ExplorerWindow");
        }
    }

    /// <summary>
    /// Loads collections from all databases and populates the Copy/Move dropdown menus.
    /// </summary>
    private async Task LoadCollectionDropdownsAsync()
    {
        try
        {
            _logger?.LogInformation("Starting to load collection dropdowns...");

            CopyToCollectionDropdown.Items.Clear();
            MoveToCollectionDropdown.Items.Clear();

            using var scope = _serviceProvider.CreateScope();
            var databaseManager = scope.ServiceProvider.GetRequiredService<IDatabaseManager>();

            // Get all loaded databases and their contexts
            var databaseContexts = databaseManager.GetAllDatabaseContexts().ToList();

            _logger?.LogInformation("Found {Count} database contexts", databaseContexts.Count);

            if (databaseContexts.Count == 0)
            {
                _logger?.LogWarning("No databases loaded for collection dropdowns");
                return;
            }

            // Build dropdown items for each database
            foreach (var (databaseName, context) in databaseContexts)
            {
                _logger?.LogInformation("Processing database: {DatabaseName}", databaseName);

                // Get all collections from this database, then filter non-virtual in memory
                var allCollections = await context.Collections
                    .OrderBy(p => p.SortKey)
                    .ToListAsync();

                var collections = allCollections.Where(p => !p.IsVirtual).ToList();

                _logger?.LogInformation("Found {Count} collections in {Database}", collections.Count, databaseName);

                if (collections.Count == 0)
                    continue;

                // Add database header (disabled) if we have multiple databases
                if (databaseContexts.Count > 1)
                {
                    var dbHeaderCopy = new BarButtonItem { Content = databaseName, IsEnabled = false };
                    var dbHeaderMove = new BarButtonItem { Content = databaseName, IsEnabled = false };
                    CopyToCollectionDropdown.Items.Add(dbHeaderCopy);
                    MoveToCollectionDropdown.Items.Add(dbHeaderMove);
                }

                // Add collection items
                foreach (var item in collections)
                {
                    var collectionId = item.Id; // Capture for closure

                    _logger?.LogDebug("Adding collection: {CollectionName} ({CollectionId})", item.Name, collectionId);

                    var copyItem = new BarButtonItem
                    {
                        Content = item.Name,
                        Tag = collectionId,
                    };

                    copyItem.ItemClick += async (_, _) => await CopyToCollectionAsync(collectionId);

                    var moveItem = new BarButtonItem
                    {
                        Content = item.Name,
                        Tag = collectionId,
                    };

                    moveItem.ItemClick += async (_, _) => await MoveToCollectionAsync(collectionId);

                    CopyToCollectionDropdown.Items.Add(copyItem);
                    MoveToCollectionDropdown.Items.Add(moveItem);
                }

                // Add separator between databases if we have multiple
                if (databaseContexts.Count > 1 && databaseContexts.Last().Item1 != databaseName)
                {
                    CopyToCollectionDropdown.Items.Add(new BarItemSeparator());
                    MoveToCollectionDropdown.Items.Add(new BarItemSeparator());
                }
            }

            _logger?.LogInformation("Loaded collection dropdowns: Copy={CopyCount} items, Move={MoveCount} items",
                CopyToCollectionDropdown.Items.Count, MoveToCollectionDropdown.Items.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load collection dropdowns");
        }
    }

    /// <summary>
    /// Copies selected clips to the specified collection.
    /// </summary>
    private async Task CopyToCollectionAsync(Guid collectionId)
    {
        try
        {
            var selectedClips = _viewModel.PrimaryClipList.SelectedClips;
            if (selectedClips.Count == 0)
            {
                _viewModel.SetStatus("No clips selected");
                return;
            }

            // Get database key from currently selected tree node
            var sourceDatabaseKey = GetDatabaseKeyForNode(_viewModel.CollectionTree.SelectedNode);
            if (string.IsNullOrEmpty(sourceDatabaseKey))
            {
                _logger?.LogError("Cannot copy clips: source database key not found");
                _viewModel.SetStatus("Error: source database not found");
                return;
            }

            var clipService = _serviceProvider.GetRequiredService<IClipService>();

            var copiedCount = 0;
            foreach (var item in selectedClips)
            {
                // For toolbar dropdown, we're copying within the same database
                await clipService.CopyClipAsync(sourceDatabaseKey, item.Id, collectionId);
                copiedCount++;
            }

            _viewModel.SetStatus($"Copied {copiedCount} clip(s)");
            await _viewModel.PrimaryClipList.LoadClipsAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to copy clips from toolbar dropdown");
            _viewModel.SetStatus("Error copying clips");
        }
    }

    /// <summary>
    /// Moves selected clips to the specified collection.
    /// </summary>
    private async Task MoveToCollectionAsync(Guid collectionId)
    {
        try
        {
            var selectedClips = _viewModel.PrimaryClipList.SelectedClips;
            if (selectedClips.Count == 0)
            {
                _viewModel.SetStatus("No clips selected");
                return;
            }

            // Get database key from currently selected tree node
            var sourceDatabaseKey = GetDatabaseKeyForNode(_viewModel.CollectionTree.SelectedNode);
            if (string.IsNullOrEmpty(sourceDatabaseKey))
            {
                _logger?.LogError("Cannot move clips: source database key not found");
                _viewModel.SetStatus("Error: source database not found");
                return;
            }

            var clipService = _serviceProvider.GetRequiredService<IClipService>();

            var movedCount = 0;
            foreach (var item in selectedClips)
            {
                // For toolbar dropdown, we're moving within the same database
                await clipService.MoveClipAsync(sourceDatabaseKey, item.Id, collectionId);
                movedCount++;
            }

            _viewModel.SetStatus($"Moved {movedCount} clip(s)");
            await _viewModel.PrimaryClipList.LoadClipsAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to move clips from toolbar dropdown");
            _viewModel.SetStatus("Error moving clips");
        }
    }

    /// <summary>
    /// Gets the database configuration key for a tree node by traversing up to the database node.
    /// </summary>
    private static string? GetDatabaseKeyForNode(TreeNodeBase? node)
    {
        if (node == null)
            return null;

        // Traverse up the tree to find the DatabaseTreeNode
        var current = node;
        while (current != null)
        {
            if (current is DatabaseTreeNode dbNode)
                return dbNode.DatabasePath;

            current = current.Parent;
        }

        return null;
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
