using System.Windows;
using System.Windows.Input;
using ClipMate.App.ViewModels;
using ClipMate.App.Views;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using ClipMate.Data.Services;
using Microsoft.Extensions.Logging;

namespace ClipMate.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ClipListViewModel _clipListViewModel;
    private readonly PreviewPaneViewModel _previewPaneViewModel;
    private readonly CollectionTreeViewModel _collectionTreeViewModel;
    private readonly SearchViewModel _searchViewModel;
    private readonly ClipboardCoordinator _clipboardCoordinator;
    private readonly ILogger<MainWindow>? _logger;
    private readonly IServiceProvider _serviceProvider;

    public MainWindow(
        CollectionTreeViewModel collectionTreeViewModel,
        SearchViewModel searchViewModel,
        IClipService clipService,
        ClipboardCoordinator clipboardCoordinator,
        IServiceProvider serviceProvider,
        ILogger<MainWindow>? logger = null)
    {
        InitializeComponent();
        
        _collectionTreeViewModel = collectionTreeViewModel ?? throw new ArgumentNullException(nameof(collectionTreeViewModel));
        _searchViewModel = searchViewModel ?? throw new ArgumentNullException(nameof(searchViewModel));
        _clipboardCoordinator = clipboardCoordinator ?? throw new ArgumentNullException(nameof(clipboardCoordinator));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger;
        
        // Create ViewModels with real services from DI
        var mainWindowViewModel = new MainWindowViewModel();
        _clipListViewModel = new ClipListViewModel(clipService);
        _previewPaneViewModel = new PreviewPaneViewModel();
        
        // Set DataContext for the window
        DataContext = mainWindowViewModel;
        
        // Set CollectionTree DataContext
        CollectionTree.DataContext = _collectionTreeViewModel;
        
        // Set SearchPanel DataContext
        SearchPanel.DataContext = _searchViewModel;
        
        // Load initial data
        Loaded += MainWindow_Loaded;
        
        // Wire up search results to clip list
        _searchViewModel.PropertyChanged += SearchViewModel_PropertyChanged;
        
        // Add keyboard shortcut for Text Tools (Ctrl+T)
        PreviewKeyDown += MainWindow_PreviewKeyDown;
    }
    
    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
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
        if (sender is not System.Windows.Controls.MenuItem menuItem || menuItem.Tag is not Template template)
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
    /// Subscribes to clipboard coordinator events to update UI when new clips are captured.
    /// </summary>
    private void SubscribeToClipboardEvents()
    {
        // The ClipboardCoordinator saves clips to the database via ClipService
        // We need to poll or listen for new clips. For now, we'll implement a simple polling mechanism.
        // TODO: Consider implementing an event on ClipService when clips are added.
        
        // For immediate feedback, we can use a DispatcherTimer to periodically refresh
        var refreshTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2) // Refresh every 2 seconds
        };
        
        refreshTimer.Tick += async (s, e) =>
        {
            try
            {
                // Only refresh if we're not already loading
                if (!_clipListViewModel.IsLoading)
                {
                    await _clipListViewModel.RefreshAsync();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to refresh clip list");
            }
        };
        
        refreshTimer.Start();
        _logger?.LogInformation("Clipboard event subscription active (polling mode)");
    }

    private void UpdateClipCount()
    {
        // Update status bar with clip count
        if (FindName("StatusBarRight") is System.Windows.Controls.TextBlock statusText)
        {
            var count = _clipListViewModel.Clips.Count;
            
            if (ClipDataGrid.SelectedItem is Clip selectedClip)
            {
                var bytes = selectedClip.TextContent?.Length ?? 0;
                var chars = selectedClip.TextContent?.Length ?? 0;
                var words = selectedClip.TextContent?.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length ?? 0;
                
                statusText.Text = $"{count} clips | {bytes} bytes, {chars} chars, {words} words";
            }
            else
            {
                statusText.Text = $"{count} clips | 0 bytes, 0 chars, 0 words";
            }
        }
    }

    private void ClipDataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // Update preview when selection changes
        if (ClipDataGrid.SelectedItem is Clip selectedClip)
        {
            _previewPaneViewModel.SetClip(selectedClip);
            UpdatePreviewPane(selectedClip);
            UpdateClipCount(); // Update status bar with selected clip info
        }
        else
        {
            _previewPaneViewModel.SetClip(null);
            PreviewTextBlock.Text = "Select a clip to preview...";
            UpdateClipCount();
        }
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
        if (FindName("StatusBarRight") is System.Windows.Controls.TextBlock statusText)
        {
            var resultCount = _searchViewModel.SearchResults.Count;
            var totalCount = _clipListViewModel.Clips.Count;
            statusText.Text = $"Search: {resultCount} of {totalCount} clips";
        }
    }

    private void UpdatePreviewPane(Clip clip)
    {
        // Update the preview text based on clip type
        if (clip.Type == ClipType.Text && !string.IsNullOrEmpty(clip.TextContent))
        {
            PreviewTextBlock.Text = clip.TextContent;
            PreviewTextBlock.FontStyle = FontStyles.Normal;
            PreviewTextBlock.Foreground = System.Windows.Media.Brushes.Black;
        }
        else if (clip.Type == ClipType.Html && !string.IsNullOrEmpty(clip.HtmlContent))
        {
            PreviewTextBlock.Text = $"[HTML Content]\n\n{clip.HtmlContent}";
            PreviewTextBlock.FontStyle = FontStyles.Normal;
            PreviewTextBlock.Foreground = System.Windows.Media.Brushes.Black;
        }
        else if (clip.Type == ClipType.Image)
        {
            PreviewTextBlock.Text = "[Image Preview]\n\nImage preview will be displayed here.";
            PreviewTextBlock.FontStyle = FontStyles.Italic;
            PreviewTextBlock.Foreground = System.Windows.Media.Brushes.Gray;
        }
        else
        {
            PreviewTextBlock.Text = "No preview available for this clip type.";
            PreviewTextBlock.FontStyle = FontStyles.Italic;
            PreviewTextBlock.Foreground = System.Windows.Media.Brushes.Gray;
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (ClipDataGrid.SelectedItem is Clip selectedClip && !string.IsNullOrEmpty(selectedClip.TextContent))
        {
            try
            {
                Clipboard.SetText(selectedClip.TextContent);
                if (FindName("StatusBarLeft") is System.Windows.Controls.TextBlock statusLeft)
                {
                    statusLeft.Text = "Copied to clipboard";
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to copy clip to clipboard");
                MessageBox.Show($"Failed to copy: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void PreviewTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not string tabType)
        {
            return;
        }

        // Update preview based on selected tab
        if (ClipDataGrid.SelectedItem is Clip selectedClip)
        {
            switch (tabType)
            {
                case "Text":
                    PreviewTextBlock.Text = selectedClip.TextContent ?? "(No text content)";
                    break;
                case "RichText":
                    PreviewTextBlock.Text = selectedClip.RtfContent ?? "(No RTF content)";
                    break;
                case "Html":
                    PreviewTextBlock.Text = selectedClip.HtmlContent ?? "(No HTML content)";
                    break;
            }
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (ClipDataGrid.SelectedItem is Clip selectedClip)
        {
            var result = MessageBox.Show(
                "Are you sure you want to delete this clip?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _clipListViewModel.Clips.Remove(selectedClip);
                
                if (FindName("StatusBarLeft") is System.Windows.Controls.TextBlock statusLeft)
                {
                    statusLeft.Text = "Clip deleted";
                }
            }
        }
    }
}

/// <summary>
/// Temporary mock ClipService for development/testing.
/// TODO: Replace with real service implementation.
/// </summary>
internal class MockClipService : IClipService
{
    private readonly List<Clip> _sampleClips = new()
    {
        new Clip
        {
            Id = Guid.NewGuid(),
            Type = ClipType.Text,
            TextContent = "Sample clipboard text content...",
            CapturedAt = DateTime.Now.AddMinutes(-5),
            ContentHash = "hash1"
        },
        new Clip
        {
            Id = Guid.NewGuid(),
            Type = ClipType.Text,
            TextContent = "Another clipboard entry from earlier today",
            CapturedAt = DateTime.Now.AddHours(-2),
            ContentHash = "hash2"
        },
        new Clip
        {
            Id = Guid.NewGuid(),
            Type = ClipType.Text,
            TextContent = "https://github.com/example/repo - Useful repository link",
            CapturedAt = DateTime.Now.AddHours(-4),
            ContentHash = "hash3"
        }
    };

    public Task<Clip?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_sampleClips.FirstOrDefault(c => c.Id == id));
    }

    public Task<IReadOnlyList<Clip>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Clip>>(_sampleClips.Take(count).ToList());
    }

    public Task<IReadOnlyList<Clip>> GetByCollectionAsync(Guid collectionId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Clip>>(Array.Empty<Clip>());
    }

    public Task<IReadOnlyList<Clip>> GetByFolderAsync(Guid folderId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Clip>>(Array.Empty<Clip>());
    }

    public Task<IReadOnlyList<Clip>> GetFavoritesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Clip>>(Array.Empty<Clip>());
    }

    public Task<Clip> CreateAsync(Clip clip, CancellationToken cancellationToken = default)
    {
        _sampleClips.Add(clip);
        return Task.FromResult(clip);
    }

    public Task UpdateAsync(Clip clip, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var clip = _sampleClips.FirstOrDefault(c => c.Id == id);
        if (clip != null)
        {
            _sampleClips.Remove(clip);
        }
        return Task.CompletedTask;
    }

    public Task<int> DeleteOlderThanAsync(DateTime olderThan, CancellationToken cancellationToken = default)
    {
        var toDelete = _sampleClips.Where(c => c.CapturedAt < olderThan).ToList();
        foreach (var clip in toDelete)
        {
            _sampleClips.Remove(clip);
        }
        return Task.FromResult(toDelete.Count);
    }

    public Task<bool> IsDuplicateAsync(string contentHash, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_sampleClips.Any(c => c.ContentHash == contentHash));
    }
}

