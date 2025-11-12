using System.Windows;
using System.Windows.Input;
using ClipMate.App.ViewModels;
using ClipMate.App.Views;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
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
    private readonly ILogger<MainWindow>? _logger;
    private readonly IServiceProvider _serviceProvider;

    public MainWindow(
        CollectionTreeViewModel collectionTreeViewModel,
        SearchViewModel searchViewModel,
        IServiceProvider serviceProvider,
        ILogger<MainWindow>? logger = null)
    {
        InitializeComponent();
        
        _collectionTreeViewModel = collectionTreeViewModel ?? throw new ArgumentNullException(nameof(collectionTreeViewModel));
        _searchViewModel = searchViewModel ?? throw new ArgumentNullException(nameof(searchViewModel));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger;
        
        // Initialize ViewModels with mock services for now
        // TODO: Replace with real DI container when implementing full application
        var mockClipService = new MockClipService();
        
        // Create ViewModels
        var mainWindowViewModel = new MainWindowViewModel();
        _clipListViewModel = new ClipListViewModel(mockClipService);
        _previewPaneViewModel = new PreviewPaneViewModel();
        
        // Set DataContext for the window
        DataContext = mainWindowViewModel;
        
        // Set CollectionTree DataContext
        CollectionTree.DataContext = _collectionTreeViewModel;
        
        // Set SearchPanel DataContext
        SearchPanel.DataContext = _searchViewModel;
        
        // Load initial data
        Loaded += MainWindow_Loaded;
        
        // Wire up selection changed event
        ClipListBox.SelectionChanged += ClipListBox_SelectionChanged;
        
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
        
        // Load clips when window is loaded
        await _clipListViewModel.LoadClipsAsync(50);
        
        // Bind the clip list to the ListBox
        ClipListBox.ItemsSource = _clipListViewModel.Clips;
        
        // Update clip count
        UpdateClipCount();
        
        // Subscribe to collection changed events
        _clipListViewModel.Clips.CollectionChanged += (s, args) => UpdateClipCount();
    }

    private void UpdateClipCount()
    {
        // Find the status TextBlock and update it
        var statusBorder = FindName("ClipStatusBorder") as System.Windows.Controls.Border;
        if (statusBorder?.Child is System.Windows.Controls.TextBlock statusText)
        {
            statusText.Text = $"Total clips: {_clipListViewModel.Clips.Count}";
        }
    }

    private void ClipListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // Update preview when selection changes
        if (ClipListBox.SelectedItem is Clip selectedClip)
        {
            _previewPaneViewModel.SetClip(selectedClip);
            UpdatePreviewPane(selectedClip);
        }
        else
        {
            _previewPaneViewModel.SetClip(null);
            PreviewTextBlock.Text = "Select a clip to preview...";
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
                ClipListBox.ItemsSource = _searchViewModel.SearchResults;
                UpdateSearchClipCount();
            }
            else if (!string.IsNullOrEmpty(_searchViewModel.SearchText))
            {
                // Search was performed but no results found
                ClipListBox.ItemsSource = _searchViewModel.SearchResults;
                UpdateSearchClipCount();
            }
            else
            {
                // Search was cleared, restore original clip list
                ClipListBox.ItemsSource = _clipListViewModel.Clips;
                UpdateClipCount();
            }
        }
    }

    private void UpdateSearchClipCount()
    {
        // Find the status TextBlock and update it
        var statusBorder = FindName("ClipStatusBorder") as System.Windows.Controls.Border;
        if (statusBorder?.Child is System.Windows.Controls.TextBlock statusText)
        {
            statusText.Text = $"Search results: {_searchViewModel.SearchResults.Count} of {_clipListViewModel.Clips.Count} clips";
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
        if (ClipListBox.SelectedItem is Clip selectedClip && !string.IsNullOrEmpty(selectedClip.TextContent))
        {
            try
            {
                Clipboard.SetText(selectedClip.TextContent);
                MessageBox.Show("Copied to clipboard!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (ClipListBox.SelectedItem is Clip selectedClip)
        {
            MessageBox.Show("Edit functionality will be implemented in a future update.", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (ClipListBox.SelectedItem is Clip selectedClip)
        {
            var result = MessageBox.Show(
                "Are you sure you want to delete this clip?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _clipListViewModel.Clips.Remove(selectedClip);
                MessageBox.Show("Clip deleted successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
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

