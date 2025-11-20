using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the main application window.
/// Orchestrates child ViewModels and coordinates the three-pane interface.
/// Manages window state and application-level concerns.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly ICollectionService _collectionService;
    private readonly IFolderService _folderService;
    private readonly ILogger<MainWindowViewModel>? _logger;

    #region Child ViewModels

    /// <summary>
    /// ViewModel for the collection tree (left pane).
    /// </summary>
    public CollectionTreeViewModel CollectionTree { get; }

    /// <summary>
    /// ViewModel for the primary clip list (middle pane).
    /// </summary>
    public ClipListViewModel PrimaryClipList { get; }

    /// <summary>
    /// ViewModel for the preview pane (right pane).
    /// </summary>
    public PreviewPaneViewModel PreviewPane { get; }

    /// <summary>
    /// ViewModel for the search panel.
    /// </summary>
    public SearchViewModel Search { get; }

    #endregion

    #region Window State

    [ObservableProperty]
    private string _title = "ClipMate";

    [ObservableProperty]
    private double _windowWidth = 1200;

    [ObservableProperty]
    private double _windowHeight = 800;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private double _leftPaneWidth = 250;

    [ObservableProperty]
    private double _rightPaneWidth = 400;

    [ObservableProperty]
    private bool _isDualClipListMode = false;

    [ObservableProperty]
    private double _primaryClipListHeight = 350;

    #endregion

    public MainWindowViewModel(
        CollectionTreeViewModel collectionTreeViewModel,
        ClipListViewModel clipListViewModel,
        PreviewPaneViewModel previewPaneViewModel,
        SearchViewModel searchViewModel,
        ICollectionService collectionService,
        IFolderService folderService,
        ILogger<MainWindowViewModel>? logger = null)
    {
        CollectionTree = collectionTreeViewModel ?? throw new ArgumentNullException(nameof(collectionTreeViewModel));
        PrimaryClipList = clipListViewModel ?? throw new ArgumentNullException(nameof(clipListViewModel));
        PreviewPane = previewPaneViewModel ?? throw new ArgumentNullException(nameof(previewPaneViewModel));
        Search = searchViewModel ?? throw new ArgumentNullException(nameof(searchViewModel));
        _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
        _folderService = folderService ?? throw new ArgumentNullException(nameof(folderService));
        _logger = logger;
    }

    /// <summary>
    /// Initializes the main window and all child ViewModels.
    /// Should be called after the window is loaded.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            SetBusy(true, "Loading collections...");

            // Load collections and folders
            _logger?.LogInformation("Loading collections and folders");
            await CollectionTree.LoadAsync();

            // Expand the first database and find the Inbox collection
            var firstDatabase = CollectionTree.RootNodes.OfType<DatabaseTreeNode>().FirstOrDefault();
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
                        CollectionTree.SelectedNode = targetCollection;

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
                            CollectionTree.SelectedNode = inboxFolder;

                            // Set Inbox folder as the active folder for new clips
                            await _folderService.SetActiveAsync(inboxFolder.Folder.Id);

                            _logger?.LogInformation("Inbox folder selected and set as active for new clips");
                        }
                        else
                        {
                            // No Inbox folder found, select the collection itself
                            targetCollection.IsSelected = true;
                            CollectionTree.SelectedNode = targetCollection;

                            _logger?.LogInformation("No Inbox folder found in collection {CollectionName}, selected collection", targetCollection.Name);
                        }
                    }
                }
            }

            // Load initial clips
            SetBusy(true, "Loading clips...");
            await PrimaryClipList.LoadClipsAsync(50);

            _logger?.LogInformation("MainWindow initialization completed successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize MainWindow");
            SetStatus("Error loading data");
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>
    /// Sets the status message displayed in the status bar.
    /// </summary>
    /// <param name="message">The status message to display.</param>
    public void SetStatus(string message)
    {
        StatusMessage = message ?? string.Empty;
    }

    /// <summary>
    /// Sets the busy state and optional status message.
    /// </summary>
    /// <param name="isBusy">Whether the application is busy.</param>
    /// <param name="message">Optional status message to display when busy.</param>
    public void SetBusy(bool isBusy, string? message = null)
    {
        IsBusy = isBusy;
        StatusMessage = isBusy ? (message ?? string.Empty) : string.Empty;
    }
}
