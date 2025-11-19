using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClipMate.App.Services;
using ClipMate.App.ViewModels;
using ClipMate.App.Views;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using ClipMate.Data.Services;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Grid;
using Microsoft.Extensions.Logging;
using MessageBox = System.Windows.MessageBox;
using Clipboard = System.Windows.Clipboard;
using Application = System.Windows.Application;

namespace ClipMate.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window, IWindow
{
    private readonly MainWindowViewModel _mainWindowViewModel;
    private readonly ClipListViewModel _clipListViewModel;
    private readonly PreviewPaneViewModel _previewPaneViewModel;
    private readonly CollectionTreeViewModel _collectionTreeViewModel;
    private readonly SearchViewModel _searchViewModel;
    private readonly ClipboardCoordinator _clipboardCoordinator;
    private readonly PowerPasteCoordinator _powerPasteCoordinator;
    private readonly IClipService _clipService;
    private readonly IFolderService _folderService;
    private readonly ICollectionService _collectionService;
    private readonly ILogger<MainWindow>? _logger;
    private readonly IServiceProvider _serviceProvider;
    private bool _isExiting = false;

    public MainWindow(
        MainWindowViewModel mainWindowViewModel,
        CollectionTreeViewModel collectionTreeViewModel,
        SearchViewModel searchViewModel,
        IClipService clipService,
        IFolderService folderService,
        ICollectionService collectionService,
        ClipboardCoordinator clipboardCoordinator,
        PowerPasteCoordinator powerPasteCoordinator,
        IServiceProvider serviceProvider,
        CommunityToolkit.Mvvm.Messaging.IMessenger messenger,
        ILogger<MainWindow>? logger = null)
    {
        InitializeComponent();

        _mainWindowViewModel = mainWindowViewModel ?? throw new ArgumentNullException(nameof(mainWindowViewModel));
        _collectionTreeViewModel = collectionTreeViewModel ?? throw new ArgumentNullException(nameof(collectionTreeViewModel));
        _searchViewModel = searchViewModel ?? throw new ArgumentNullException(nameof(searchViewModel));
        _clipService = clipService ?? throw new ArgumentNullException(nameof(clipService));
        _folderService = folderService ?? throw new ArgumentNullException(nameof(folderService));
        _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
        _clipboardCoordinator = clipboardCoordinator ?? throw new ArgumentNullException(nameof(clipboardCoordinator));
        _powerPasteCoordinator = powerPasteCoordinator ?? throw new ArgumentNullException(nameof(powerPasteCoordinator));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger;
        
        // Create ViewModels with real services from DI
        _clipListViewModel = new ClipListViewModel(clipService, messenger);
        _previewPaneViewModel = new PreviewPaneViewModel();
        
        // Set DataContext for the window
        DataContext = _mainWindowViewModel;
        
        // Set CollectionTree DataContext
        CollectionTree.DataContext = _collectionTreeViewModel;
        
        // Set ClipDataGrid DataContext
        ClipDataGrid.DataContext = _clipListViewModel;
        
        // Load initial data
        Loaded += MainWindow_Loaded;
        
        // Wire up closing event to minimize to tray
        Closing += MainWindow_Closing;
        
        // Wire up search results to clip list
        _searchViewModel.PropertyChanged += SearchViewModel_PropertyChanged;
        
        // Wire collection tree selection to clip list
        _collectionTreeViewModel.SelectedNodeChanged += CollectionTreeViewModel_SelectedNodeChanged;
        
        // Wire clip list selection to preview pane
        _clipListViewModel.PropertyChanged += ClipListViewModel_PropertyChanged;
        
        // Add keyboard shortcut for Text Tools (Ctrl+T)
        PreviewKeyDown += MainWindow_PreviewKeyDown;
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        // Hide window and taskbar icon when minimized (will remain visible in system tray)
        if (WindowState == WindowState.Minimized)
        {
            Hide();
            ShowInTaskbar = false;
            _logger?.LogDebug("MainWindow minimized to tray");
        }
        else if (WindowState == WindowState.Normal || WindowState == WindowState.Maximized)
        {
            ShowInTaskbar = true;
        }
    }

    private void ShowMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Show();
        Activate();
        WindowState = WindowState.Normal;
        _logger?.LogDebug("MainWindow shown from tray menu");
    }

    /// <summary>
    /// Handles collection tree selection changes to load clips
    /// </summary>
    private async void CollectionTreeViewModel_SelectedNodeChanged(object? sender, (Guid CollectionId, Guid? FolderId) e)
    {
        try
        {
            _logger?.LogInformation("Collection tree selection changed: Collection={CollectionId}, Folder={FolderId}", 
                e.CollectionId, e.FolderId);

            // Set the active folder for new clipboard captures
            await _folderService.SetActiveAsync(e.FolderId);

            if (e.FolderId.HasValue)
            {
                // Load clips for the selected folder
                await _clipListViewModel.LoadClipsByFolderAsync(e.CollectionId, e.FolderId.Value);
            }
            else
            {
                // Load clips for the selected collection
                await _clipListViewModel.LoadClipsByCollectionAsync(e.CollectionId);
            }

            UpdateClipCount();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load clips for selected node");
        }
    }

    /// <summary>
    /// Handles clip list selection changes to update preview pane
    /// </summary>
    private void ClipListViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ClipListViewModel.SelectedClip))
        {
            _logger?.LogDebug("Clip selection changed");
            _previewPaneViewModel.SetClip(_clipListViewModel.SelectedClip);
        }
    }

    /// <summary>
    /// Prepares the window for application exit (skips minimize to tray behavior)
    /// </summary>
    public void PrepareForExit()
    {
        _isExiting = true;
    }
    
    private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.T && (Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
        {
            ShowTextTools();
            e.Handled = true;
        }
    }
    
    private void TextTools_Click(object sender, RoutedEventArgs e)
    {
        ShowTextTools();
    }
    
    private void ShowTextTools()
    {
        try
        {
            if (_serviceProvider.GetService(typeof(TextToolsDialog)) is TextToolsDialog textToolsDialog)
            {
                textToolsDialog.Owner = this;
                textToolsDialog.ShowDialog();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to show Text Tools dialog");
            MessageBox.Show($"Failed to open Text Tools: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ManageTemplates_Click(object sender, RoutedEventArgs e)
    {
        ShowManageTemplates();
    }

    private void ShowManageTemplates()
    {
        try
        {
            if (_serviceProvider.GetService(typeof(TemplateEditorDialog)) is TemplateEditorDialog templateEditorDialog)
            {
                templateEditorDialog.Owner = this;
                templateEditorDialog.ShowDialog();

                // Reload template menu after dialog closes
                LoadTemplateMenu();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to show Template Editor dialog");
            MessageBox.Show($"Failed to open Template Editor: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void LoadTemplateMenu()
    {
        try
        {
            // Get the Insert Template menu item by name
            if (FindName("InsertTemplateMenu") is not System.Windows.Controls.MenuItem insertTemplateMenu)
            {
                return;
            }

            // Clear existing items
            insertTemplateMenu.Items.Clear();

            // Get template service from DI
            if (_serviceProvider.GetService(typeof(ITemplateService)) is not ITemplateService templateService)
            {
                _logger?.LogWarning("TemplateService not available in DI container");
                return;
            }

            // Load templates
            var templates = await templateService.GetAllAsync();

            if (templates.Count == 0)
            {
                var emptyItem = new System.Windows.Controls.MenuItem
                {
                    Header = "(No templates available)",
                    IsEnabled = false
                };
                insertTemplateMenu.Items.Add(emptyItem);
            }
            else
            {
                // Add template items
                foreach (var template in templates.OrderBy(t => t.Name))
                {
                    var menuItem = new System.Windows.Controls.MenuItem
                    {
                        Header = template.Name,
                        Tag = template,
                        ToolTip = template.Description
                    };
                    menuItem.Click += TemplateMenuItem_Click;
                    insertTemplateMenu.Items.Add(menuItem);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load template menu");
        }
    }

    private async void TemplateMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.MenuItem { Tag: Template template })
        {
            return;
        }

        try
        {
            // Get template service
            if (_serviceProvider.GetService(typeof(ITemplateService)) is not ITemplateService templateService)
            {
                MessageBox.Show("Template service not available", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Extract variables from template
            var variables = templateService.ExtractVariables(template.Content);
            var customVariables = new Dictionary<string, string>();

            // Prompt for PROMPT variables
            if (variables != null)
            {
                foreach (var variable in variables)
                {
                    // Check if it's a PROMPT variable
                    if (variable.StartsWith("PROMPT:", StringComparison.OrdinalIgnoreCase))
                    {
                        var label = variable[7..]; // Remove "PROMPT:" prefix
                        var promptDialog = new PromptDialog(label)
                        {
                            Owner = this
                        };

                        if (promptDialog.ShowDialog() == true && promptDialog.UserInput != null)
                        {
                            customVariables[variable] = promptDialog.UserInput;
                        }
                        else
                        {
                            // User cancelled, abort template expansion
                            return;
                        }
                    }
                }
            }

            // Expand template
            var expandedText = await templateService.ExpandTemplateAsync(template.Id, customVariables);

            // Insert into clipboard or current focus
            if (!string.IsNullOrEmpty(expandedText))
            {
                Clipboard.SetText(expandedText);
                MessageBox.Show($"Template '{template.Name}' has been copied to clipboard!", 
                    "Template Inserted", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to insert template: {TemplateName}", template.Name);
            MessageBox.Show($"Failed to insert template: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Load collections and folders
            _logger?.LogInformation("Loading collections and folders");
            await _collectionTreeViewModel.LoadAsync();
            
            // Expand the first database and find the Inbox collection
            var firstDatabase = _collectionTreeViewModel.RootNodes.OfType<DatabaseTreeNode>().FirstOrDefault();
            if (firstDatabase != null)
            {
                firstDatabase.IsExpanded = true;
                
                // Try to find the Inbox collection (default collection)
                var inboxCollection = firstDatabase.Children.OfType<CollectionTreeNode>()
                    .FirstOrDefault(c => c.Name.Equals("Inbox", StringComparison.OrdinalIgnoreCase));
                
                // If no Inbox collection exists, fall back to the first collection
                var targetCollection = inboxCollection ?? firstDatabase.Children.OfType<CollectionTreeNode>().FirstOrDefault();
                
                if (targetCollection != null)
                {
                    targetCollection.IsExpanded = true;
                    
                    // Check if this is a default collection (Inbox, Safe, Overflow)
                    bool isDefaultCollection = targetCollection.Name.Equals("Inbox", StringComparison.OrdinalIgnoreCase) ||
                                              targetCollection.Name.Equals("Safe", StringComparison.OrdinalIgnoreCase) ||
                                              targetCollection.Name.Equals("Overflow", StringComparison.OrdinalIgnoreCase);
                    
                    if (isDefaultCollection)
                    {
                        // Default collections don't have folders - select the collection itself
                        targetCollection.IsSelected = true;
                        _collectionTreeViewModel.SelectedNode = targetCollection;
                        
                        _logger?.LogInformation("{CollectionName} collection selected (default collection, no folders)", targetCollection.Name);
                    }
                    else
                    {
                        // User-defined collection - try to find an Inbox folder within it
                        var inboxFolder = targetCollection.Children.OfType<FolderTreeNode>()
                            .FirstOrDefault(f => f.Name.Equals("Inbox", StringComparison.OrdinalIgnoreCase));
                        
                        if (inboxFolder != null)
                        {
                            inboxFolder.IsSelected = true;
                            _collectionTreeViewModel.SelectedNode = inboxFolder;
                            
                            // Set Inbox folder as the active folder for new clips
                            await _folderService.SetActiveAsync(inboxFolder.Folder.Id);
                            
                            _logger?.LogInformation("Inbox folder selected and set as active for new clips");
                        }
                        else
                        {
                            // No Inbox folder found, select the collection itself
                            targetCollection.IsSelected = true;
                            _collectionTreeViewModel.SelectedNode = targetCollection;
                            
                            _logger?.LogInformation("No Inbox folder found in collection {CollectionName}, selected collection", targetCollection.Name);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load collections and folders");
        }
        
        // Set the DataContext for ClipDataGrid to enable binding
        ClipDataGrid.DataContext = _clipListViewModel;
        
        // Load clips when window is loaded
        await _clipListViewModel.LoadClipsAsync(50);
        
        // Update clip count
        UpdateClipCount();
        
        // Subscribe to collection changed events
        _clipListViewModel.Clips.CollectionChanged += (s, args) => UpdateClipCount();

        // Subscribe to clipboard coordinator to refresh UI when new clips are captured
        SubscribeToClipboardEvents();

        // Load template menu
        LoadTemplateMenu();
    }

    /// <summary>
    /// Handles File → Exit menu click
    /// </summary>
    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        _logger?.LogInformation("Exit menu clicked - shutting down application");
        _isExiting = true;
        System.Windows.Application.Current.Shutdown();
    }

    /// <summary>
    /// Handles the Closing event to minimize to tray instead of exiting.
    /// Hold Shift while closing to force exit.
    /// </summary>
    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // If already exiting (from File→Exit or tray menu), allow it
        if (_isExiting)
        {
            _logger?.LogInformation("MainWindow closing - application is exiting");
            
            // Unsubscribe from events to prevent memory leaks
            _clipService.ClipAdded -= OnClipAdded;
            
            return;
        }

        // Check if Shift key is held - if so, allow actual exit
        if ((Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) == System.Windows.Input.ModifierKeys.Shift)
        {
            _logger?.LogInformation("MainWindow closing with Shift key - allowing exit");
            _isExiting = true;
            
            // Unsubscribe from events to prevent memory leaks
            _clipService.ClipAdded -= OnClipAdded;
            
            return;
        }

        // Cancel the close and hide the window instead
        e.Cancel = true;
        Hide();
        _logger?.LogInformation("MainWindow minimized to system tray");
    }

    /// <summary>
    /// Subscribes to clipboard coordinator events to update UI when new clips are captured.
    /// </summary>
    private void SubscribeToClipboardEvents()
    {
        // Subscribe to the ClipAdded event to update the UI in real-time
        _clipService.ClipAdded += OnClipAdded;
        _logger?.LogInformation("Subscribed to ClipService.ClipAdded event");
    }

    /// <summary>
    /// Handles the ClipAdded event by adding the new clip to the observable collection.
    /// </summary>
    private void OnClipAdded(object? sender, Clip clip)
    {
        // Ensure we're on the UI thread
        Dispatcher.Invoke(() =>
        {
            try
            {
                // Only add the clip if it belongs to the currently displayed collection/folder
                bool shouldAdd = false;

                if (_clipListViewModel.CurrentFolderId.HasValue)
                {
                    // We're viewing a specific folder - only add if clip is in that folder
                    shouldAdd = clip.FolderId == _clipListViewModel.CurrentFolderId.Value;
                }
                else if (_clipListViewModel.CurrentCollectionId.HasValue)
                {
                    // We're viewing a collection - only add if clip is in that collection
                    shouldAdd = clip.CollectionId == _clipListViewModel.CurrentCollectionId.Value;
                }
                else
                {
                    // We're viewing "all clips" - always add
                    shouldAdd = true;
                }

                if (shouldAdd)
                {
                    // Insert at the beginning since clips are ordered by date descending
                    _clipListViewModel.Clips.Insert(0, clip);
                    _logger?.LogDebug("Added new clip {ClipId} to UI", clip.Id);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to add clip to UI");
            }
        });
    }

    private void UpdateClipCount()
    {
        // Update status bar with clip count
        var count = _clipListViewModel.Clips.Count;
        
        if (ClipDataGrid.SelectedItem is Clip selectedClip)
        {
            var bytes = selectedClip.TextContent?.Length ?? 0;
            var chars = selectedClip.TextContent?.Length ?? 0;
            var words = selectedClip.TextContent?.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries).Length ?? 0;
            
            StatusBarRight.Text = $"{count} clips | {bytes} bytes, {chars} chars, {words} words";
        }
        else
        {
            StatusBarRight.Text = $"{count} clips | 0 bytes, 0 chars, 0 words";
        }
    }

    private void ClipDataGrid_SelectionChanged(object sender, CurrentItemChangedEventArgs e)
    {
        // DevExpress GridControl uses CurrentItem
        var selectedClip = ClipDataGrid.CurrentItem as Clip;
        
        // Update the ViewModel's SelectedClip property
        _clipListViewModel.SelectedClip = selectedClip;
        
        // Update clip count with selected clip info
        UpdateClipCount();
    }

    private void SearchViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // When search results change, update the clip list
        if (e.PropertyName == nameof(SearchViewModel.SearchResults))
        {
            if (_searchViewModel.SearchResults.Count > 0)
            {
                // Display search results in the clip list
                ClipDataGrid.ItemsSource = _searchViewModel.SearchResults;
                UpdateSearchClipCount();
            }
            else if (!string.IsNullOrEmpty(_searchViewModel.SearchText))
            {
                // Search was performed but no results found
                ClipDataGrid.ItemsSource = _searchViewModel.SearchResults;
                UpdateSearchClipCount();
            }
            else
            {
                // Search was cleared, restore original clip list
                ClipDataGrid.ItemsSource = _clipListViewModel.Clips;
                UpdateClipCount();
            }
        }
    }

    private void UpdateSearchClipCount()
    {
        // Update status bar for search results
        var resultCount = _searchViewModel.SearchResults.Count;
        var totalCount = _clipListViewModel.Clips.Count;
        StatusBarRight.Text = $"Search: {resultCount} of {totalCount} clips";
    }
}
