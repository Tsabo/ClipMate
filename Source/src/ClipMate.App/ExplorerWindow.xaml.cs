using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Media;
using ClipMate.App.Helpers;
using ClipMate.App.Services;
using ClipMate.App.ViewModels;
using ClipMate.App.Views.Dialogs;
using ClipMate.Core.Events;
using ClipMate.Core.Services;
using ClipMate.Data.Services;
using CommunityToolkit.Mvvm.Messaging;
using DevExpress.Xpf.Bars;
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
    private readonly IActiveWindowService _activeWindowService;
    private readonly IConfigurationService _configurationService;
    private readonly IDatabaseManager _databaseManager;
    private readonly ILogger<ExplorerWindow>? _logger;
    private readonly IMessenger _messenger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ITemplateService _templateService;
    private readonly ExplorerWindowViewModel _viewModel;
    private bool _isExiting;

    public ExplorerWindow(ExplorerWindowViewModel explorerWindowViewModel,
        IServiceProvider serviceProvider,
        IMessenger messenger,
        IConfigurationService configurationService,
        IDatabaseManager databaseManager,
        ITemplateService templateService,
        IActiveWindowService activeWindowService,
        ILogger<ExplorerWindow>? logger = null)
    {
        InitializeComponent();

        _viewModel = explorerWindowViewModel ?? throw new ArgumentNullException(nameof(explorerWindowViewModel));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _databaseManager = databaseManager ?? throw new ArgumentNullException(nameof(databaseManager));
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
        _activeWindowService = activeWindowService ?? throw new ArgumentNullException(nameof(activeWindowService));
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
        // Mark Explorer as the active window for event routing and dialog ownership
        _activeWindowService.ActiveWindow = ActiveWindowType.Explorer;
        _activeWindowService.DialogOwner = this;

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
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize ExplorerWindow");
        }
    }

    /// <summary>
    /// Dynamically populates Copy to Collection dropdown when opened.
    /// </summary>
    private void CopyToCollectionDropdown_GetItemData(object sender, EventArgs e)
    {
        if (sender is not BarSubItem subItem)
            return;

        try
        {
            subItem.ItemLinks.Clear();

            var databaseContexts = _databaseManager.GetAllDatabaseContexts().ToList();

            // Create a dictionary mapping database keys to display names
            var databaseNames = _configurationService.Configuration.Databases
                .ToDictionary(p => p.Key, p => p.Value.Name, StringComparer.OrdinalIgnoreCase);

            if (databaseContexts.Count == 0)
            {
                _logger?.LogWarning("No databases loaded for Copy to Collection dropdown");
                return;
            }

            foreach (var (databaseKey, context) in databaseContexts)
            {
                // Get all collections from this database, then filter non-virtual in memory
                var allCollections = context.Collections
                    .OrderBy(p => p.SortKey)
                    .ToList();

                var collections = allCollections.Where(p => !p.IsVirtual).ToList();

                if (collections.Count == 0)
                    continue;

                // Add database header (disabled) if we have multiple databases
                if (databaseContexts.Count > 1)
                {
                    // Look up the database name using the database key
                    var databaseName = databaseNames.TryGetValue(databaseKey, out var name)
                        ? name
                        : databaseKey;

                    var dbHeader = new BarButtonItem { Content = databaseName, IsEnabled = false };
                    subItem.ItemLinks.Add(dbHeader);
                }

                // Add collection items
                foreach (var item in collections)
                {
                    var collectionId = item.Id; // Capture for closure
                    var targetDatabaseKey = databaseKey; // Capture database key for closure
                    var copyItem = new BarButtonItem
                    {
                        Content = item.Name,
                        Tag = (collectionId, targetDatabaseKey), // Store both collection ID and database key
                    };

                    // Add emoji icon if available
                    if (!string.IsNullOrEmpty(item.Icon))
                    {
                        var iconExtension = new EmojiIconSourceExtension(item.Icon) { Size = 16 };
                        copyItem.Glyph = iconExtension.ProvideValue(null!) as ImageSource;
                    }

                    copyItem.ItemClick += async (_, _) => await CopyToCollectionAsync(collectionId, targetDatabaseKey);
                    subItem.ItemLinks.Add(copyItem);
                }

                // Add separator between databases if we have multiple
                if (databaseContexts.Count > 1 && databaseContexts.Last().DatabaseKey != databaseKey)
                    subItem.ItemLinks.Add(new BarItemSeparator());
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to populate Copy to Collection dropdown");
        }
    }

    /// <summary>
    /// Dynamically populates Move to Collection dropdown when opened.
    /// </summary>
    private void MoveToCollectionDropdown_GetItemData(object sender, EventArgs e)
    {
        if (sender is not BarSubItem subItem)
            return;

        try
        {
            subItem.ItemLinks.Clear();

            var databaseContexts = _databaseManager.GetAllDatabaseContexts().ToList();

            // Create a dictionary mapping database keys to display names
            var databaseNames = _configurationService.Configuration.Databases
                .ToDictionary(p => p.Key, p => p.Value.Name, StringComparer.OrdinalIgnoreCase);

            if (databaseContexts.Count == 0)
            {
                _logger?.LogWarning("No databases loaded for Move to Collection dropdown");
                return;
            }

            foreach (var (databaseKey, context) in databaseContexts)
            {
                // Get all collections from this database, then filter non-virtual in memory
                var allCollections = context.Collections
                    .OrderBy(p => p.SortKey)
                    .ToList();

                var collections = allCollections.Where(p => !p.IsVirtual).ToList();

                if (collections.Count == 0)
                    continue;

                // Add database header (disabled) if we have multiple databases
                if (databaseContexts.Count > 1)
                {
                    // Look up the database name using the database key
                    var databaseName = databaseNames.TryGetValue(databaseKey, out var name)
                        ? name
                        : databaseKey;

                    var dbHeader = new BarButtonItem { Content = databaseName, IsEnabled = false };
                    subItem.ItemLinks.Add(dbHeader);
                }

                // Add collection items
                foreach (var item in collections)
                {
                    var collectionId = item.Id; // Capture for closure
                    var targetDatabaseKey = databaseKey; // Capture database key for closure
                    var moveItem = new BarButtonItem
                    {
                        Content = item.Name,
                        Tag = (collectionId, targetDatabaseKey), // Store both collection ID and database key
                    };

                    // Add emoji icon if available
                    if (!string.IsNullOrEmpty(item.Icon))
                    {
                        var iconExtension = new EmojiIconSourceExtension(item.Icon) { Size = 16 };
                        moveItem.Glyph = iconExtension.ProvideValue(null!) as ImageSource;
                    }

                    moveItem.ItemClick += async (_, _) => await MoveToCollectionAsync(collectionId, targetDatabaseKey);
                    subItem.ItemLinks.Add(moveItem);
                }

                // Add separator between databases if we have multiple
                if (databaseContexts.Count > 1 && databaseContexts.Last().DatabaseKey != databaseKey)
                    subItem.ItemLinks.Add(new BarItemSeparator());
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to populate Move to Collection dropdown");
        }
    }

    /// <summary>
    /// Dynamically populates Template dropdown when opened.
    /// </summary>
    private async void TemplateDropdown_GetItemData(object sender, EventArgs e)
    {
        if (sender is not BarSubItem subItem)
            return;

        try
        {
            subItem.ItemLinks.Clear();

            // "No Template" option (checked when active)
            var noTemplateItem = new BarCheckItem
            {
                Content = "No Template",
                IsChecked = _templateService.ActiveTemplate == null,
            };

            noTemplateItem.ItemClick += (_, _) => _viewModel.SelectTemplate(null);
            subItem.ItemLinks.Add(noTemplateItem);

            // Load and display template files
            var templates = await _templateService.GetAllTemplatesAsync();
            foreach (var item in templates)
            {
                var templateName = item.Name; // Capture for closure
                var templateItem = new BarCheckItem
                {
                    Content = item.Name,
                    IsChecked = _templateService.ActiveTemplate?.Name == item.Name,
                };

                templateItem.ItemClick += (_, _) => _viewModel.SelectTemplate(templateName);
                subItem.ItemLinks.Add(templateItem);
            }

            // Separator before commands
            subItem.ItemLinks.Add(new BarItemSeparator());

            // Refresh Template List
            var refreshItem = new BarButtonItem { Content = "Refresh Template List" };
            refreshItem.ItemClick += async (_, _) =>
            {
                await _templateService.RefreshTemplatesAsync();
                _logger?.LogInformation("Template list refreshed");
            };

            subItem.ItemLinks.Add(refreshItem);

            // Reset Sequence
            var resetSequenceItem = new BarButtonItem { Content = "Reset Sequence" };
            resetSequenceItem.ItemClick += (_, _) =>
            {
                _viewModel.ResetTemplateSequence();
                _logger?.LogInformation("Template sequence reset to 1");
            };

            subItem.ItemLinks.Add(resetSequenceItem);

            // Open Template Directory
            var openDirItem = new BarButtonItem
            {
                Content = "Open Template Directory",
                Glyph = new EmojiIconSourceExtension("📁") { Size = 16 }.ProvideValue(null!) as ImageSource,
            };

            openDirItem.ItemClick += (_, _) =>
            {
                _templateService.OpenTemplatesDirectory();
                _logger?.LogInformation("Opened templates directory");
            };

            subItem.ItemLinks.Add(openDirItem);

            // Template Help
            var helpItem = new BarButtonItem
            {
                Content = "Template Help",
                Glyph = new EmojiIconSourceExtension("❓") { Size = 16 }.ProvideValue(null!) as ImageSource,
            };

            helpItem.ItemClick += (_, _) => MessageBox.Show(
                "Template Help: Templates allow you to merge clip data with placeholders like #TITLE#, #URL#, #DATE#, etc.",
                "Template Help", MessageBoxButton.OK, MessageBoxImage.Information);

            subItem.ItemLinks.Add(helpItem);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error populating Template dropdown");
        }
    }

    /// <summary>
    /// Copies selected clips to the specified collection.
    /// Supports both same-database and cross-database operations.
    /// </summary>
    private async Task CopyToCollectionAsync(Guid collectionId, string targetDatabaseKey)
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
                // Check if cross-database operation
                if (sourceDatabaseKey == targetDatabaseKey)
                {
                    // Same database - use simple copy
                    await clipService.CopyClipAsync(sourceDatabaseKey, item.Id, collectionId);
                }
                else
                {
                    // Cross-database - copy with ClipData and blobs
                    await clipService.CopyClipCrossDatabaseAsync(sourceDatabaseKey, item.Id, targetDatabaseKey, collectionId);
                }

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
    /// Supports both same-database and cross-database operations.
    /// Handles ClipData and blob migration for cross-database moves.
    /// </summary>
    private async Task MoveToCollectionAsync(Guid collectionId, string targetDatabaseKey)
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
                // Check if cross-database operation
                if (sourceDatabaseKey == targetDatabaseKey)
                {
                    // Same database - use simple move
                    await clipService.MoveClipAsync(sourceDatabaseKey, item.Id, collectionId);
                }
                else
                {
                    // Cross-database - move with ClipData and blobs, then delete original
                    await clipService.MoveClipCrossDatabaseAsync(sourceDatabaseKey, item.Id, targetDatabaseKey, collectionId);
                }

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
