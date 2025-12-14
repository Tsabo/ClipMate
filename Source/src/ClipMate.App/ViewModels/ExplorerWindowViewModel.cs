using ClipMate.Core.Models;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the main application window.
/// Orchestrates child ViewModels and coordinates the three-pane interface.
/// Manages window state and application-level concerns.
/// </summary>
public partial class ExplorerWindowViewModel : ObservableObject
{
    private readonly ILogger<ExplorerWindowViewModel>? _logger;
    private readonly IQuickPasteService _quickPasteService;
    private readonly IServiceProvider _serviceProvider;

    public ExplorerWindowViewModel(CollectionTreeViewModel collectionTreeViewModel,
        ClipListViewModel clipListViewModel,
        PreviewPaneViewModel previewPaneViewModel,
        SearchViewModel searchViewModel,
        QuickPasteToolbarViewModel quickPasteToolbarViewModel,
        MainMenuViewModel mainMenuViewModel,
        IServiceProvider serviceProvider,
        IQuickPasteService quickPasteService,
        ILogger<ExplorerWindowViewModel>? logger = null)
    {
        CollectionTree = collectionTreeViewModel ?? throw new ArgumentNullException(nameof(collectionTreeViewModel));
        PrimaryClipList = clipListViewModel ?? throw new ArgumentNullException(nameof(clipListViewModel));
        PreviewPane = previewPaneViewModel ?? throw new ArgumentNullException(nameof(previewPaneViewModel));
        Search = searchViewModel ?? throw new ArgumentNullException(nameof(searchViewModel));
        QuickPasteToolbarViewModel = quickPasteToolbarViewModel ?? throw new ArgumentNullException(nameof(quickPasteToolbarViewModel));
        MainMenu = mainMenuViewModel ?? throw new ArgumentNullException(nameof(mainMenuViewModel));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _quickPasteService = quickPasteService ?? throw new ArgumentNullException(nameof(quickPasteService));
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
                    .FirstOrDefault(p => p.Name.Equals("Inbox", StringComparison.OrdinalIgnoreCase));

                // If no Inbox collection exists, fall back to the first collection
                var targetCollection = inboxCollection ?? firstDatabase.Children.OfType<CollectionTreeNode>().FirstOrDefault();

                if (targetCollection != null)
                {
                    targetCollection.IsExpanded = true;

                    // Set this collection as the active collection for new clips
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var collectionService = scope.ServiceProvider.GetRequiredService<ICollectionService>();
                        // Find the database key by traversing up from target node
                        var databaseKey = GetDatabaseKeyForNode(targetCollection);
                        if (!string.IsNullOrEmpty(databaseKey))
                            await collectionService.SetActiveAsync(targetCollection.Collection.Id, databaseKey);
                        else if (_logger != null)
                            _logger.LogWarning("Could not determine database key for target collection");
                    }

                    // Check if this is a default collection (Inbox, Safe, Overflow)
                    var isDefaultCollection = targetCollection.Name.Equals("Inbox", StringComparison.OrdinalIgnoreCase) ||
                                              targetCollection.Name.Equals("Safe", StringComparison.OrdinalIgnoreCase) ||
                                              targetCollection.Name.Equals("Overflow", StringComparison.OrdinalIgnoreCase);

                    if (isDefaultCollection)
                    {
                        // Default collections don't have folders - select the collection itself
                        targetCollection.IsSelected = true;
                        CollectionTree.SelectedNode = targetCollection;

                        _logger?.LogInformation("{CollectionName} collection selected and set as active (default collection, no folders)", targetCollection.Name);
                    }
                    else
                    {
                        // User-defined collection - try to find an Inbox folder within it
                        var inboxFolder = targetCollection.Children.OfType<FolderTreeNode>()
                            .FirstOrDefault(p => p.Name.Equals("Inbox", StringComparison.OrdinalIgnoreCase));

                        if (inboxFolder != null)
                        {
                            inboxFolder.IsSelected = true;
                            CollectionTree.SelectedNode = inboxFolder;

                            // Set Inbox folder as the active folder for new clips
                            using (var scope = _serviceProvider.CreateScope())
                            {
                                var folderService = scope.ServiceProvider.GetRequiredService<IFolderService>();
                                await folderService.SetActiveAsync(inboxFolder.Folder.Id);
                            }

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

            _logger?.LogInformation("ExplorerWindow initialization completed successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize ExplorerWindow");
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
    public void SetStatus(string message) => StatusMessage = message;

    /// <summary>
    /// Sets the busy state and optional status message.
    /// </summary>
    /// <param name="isBusy">Whether the application is busy.</param>
    /// <param name="message">Optional status message to display when busy.</param>
    public void SetBusy(bool isBusy, string? message = null)
    {
        IsBusy = isBusy;
        StatusMessage = isBusy
            ? message ?? string.Empty
            : string.Empty;
    }

    #region Window Event Handlers

    /// <summary>
    /// Called when the main window is deactivated (loses focus).
    /// Captures the new foreground window as the QuickPaste target.
    /// </summary>
    public async void OnWindowDeactivated()
    {
        try
        {
            // Delay to ensure the new foreground window is fully activated
            await Task.Delay(100);
            _quickPasteService?.UpdateTarget();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating QuickPaste target on window deactivation");
        }
    }

    #endregion

    #region Helper Methods

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

    #endregion

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

    /// <summary>
    /// ViewModel for the QuickPaste toolbar.
    /// </summary>
    public QuickPasteToolbarViewModel QuickPasteToolbarViewModel { get; }

    /// <summary>
    /// Shared main menu ViewModel.
    /// </summary>
    public MainMenuViewModel MainMenu { get; }

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
    private bool _isDualClipListMode;

    [ObservableProperty]
    private double _primaryClipListHeight = 350;

    #endregion

    #region PowerPaste

    /// <summary>
    /// Indicates whether PowerPaste is currently active.
    /// </summary>
    [ObservableProperty]
    private bool _isPowerPasteActive;

    /// <summary>
    /// Current PowerPaste direction (Up or Down).
    /// </summary>
    [ObservableProperty]
    private string _powerPasteDirection = "Up";

    /// <summary>
    /// Toggles PowerPaste on/off with direction cycling.
    /// First click: activate with last direction
    /// Second click (no paste): flip direction
    /// Click after pasting: deactivate
    /// </summary>
    [RelayCommand]
    private async Task PowerPasteToggle()
    {
        if (!IsPowerPasteActive)
        {
            // Activate PowerPaste with last used direction
            await StartPowerPasteAsync(PowerPasteDirection);
        }
        else
        {
            // TODO: Check if user has pasted anything
            // For now, just toggle direction
            PowerPasteDirection = PowerPasteDirection == "Up"
                ? "Down"
                : "Up";

            _logger?.LogInformation("PowerPaste direction changed to {Direction}", PowerPasteDirection);
        }
    }

    /// <summary>
    /// Starts PowerPaste in Up direction.
    /// </summary>
    [RelayCommand]
    private async Task PowerPasteUp()
    {
        PowerPasteDirection = "Up";
        await StartPowerPasteAsync("Up");
    }

    /// <summary>
    /// Starts PowerPaste in Down direction.
    /// </summary>
    [RelayCommand]
    private async Task PowerPasteDown()
    {
        PowerPasteDirection = "Down";
        await StartPowerPasteAsync("Down");
    }

    /// <summary>
    /// Starts PowerPaste with the selected clips.
    /// </summary>
    private async Task StartPowerPasteAsync(string direction)
    {
        try
        {
            _logger?.LogInformation("Starting PowerPaste in {Direction} direction, Explode={Explode}, Loop={Loop}",
                direction, MainMenu.IsExplodeMode, MainMenu.IsLoopMode);

            // Get the selected clip(s) from ClipListView
            // Try multi-selection first, fall back to single selection
            Clip[] selectedClips;
            if (PrimaryClipList.SelectedClips.Count > 0)
                selectedClips = PrimaryClipList.SelectedClips.ToArray();
            else if (PrimaryClipList.SelectedClip != null)
                selectedClips = [PrimaryClipList.SelectedClip];
            else
            {
                _logger?.LogWarning("No clip selected for PowerPaste");
                SetStatus("Select a clip to start PowerPaste");
                return;
            }

            // Get PowerPaste service from scope
            using var scope = _serviceProvider.CreateScope();
            var powerPasteService = scope.ServiceProvider.GetRequiredService<IPowerPasteService>();

            // Start PowerPaste
            var powerPasteDirection = direction == "Up"
                ? Core.Services.PowerPasteDirection.Up
                : Core.Services.PowerPasteDirection.Down;

            await powerPasteService.StartAsync(
                selectedClips,
                powerPasteDirection,
                MainMenu.IsExplodeMode);

            IsPowerPasteActive = true;
            SetStatus($"PowerPaste active ({direction}) - Paste to advance");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start PowerPaste");
            SetStatus("Error starting PowerPaste");
        }
    }

    #endregion
}
