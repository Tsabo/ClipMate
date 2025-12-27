using System.Collections.Specialized;
using System.ComponentModel;
using ClipMate.App.Views.Dialogs;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DevExpress.Xpf.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
// For ClipApplicationService
using Application = System.Windows.Application;
using Shortcut = ClipMate.Core.Models.Shortcut;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the main application window.
/// Orchestrates child ViewModels and coordinates the three-pane interface.
/// Manages window state and application-level concerns.
/// </summary>
public partial class ExplorerWindowViewModel : ObservableObject,
    IRecipient<ClipSelectedEvent>,
    IRecipient<ImageDimensionsLoadedEvent>,
    IRecipient<ClipboardCopiedEvent>,
    IRecipient<DeleteClipsRequestedEvent>,
    IRecipient<RenameClipRequestedEvent>,
    IRecipient<CopyToCollectionRequestedEvent>,
    IRecipient<MoveToCollectionRequestedEvent>,
    IRecipient<CreateNewClipRequestedEvent>,
    IRecipient<PowerPasteUpRequestedEvent>,
    IRecipient<PowerPasteDownRequestedEvent>,
    IRecipient<PowerPasteToggleRequestedEvent>,
    IRecipient<ShowSearchWindowEvent>,
    IRecipient<ShortcutModeStatusMessage>
{
    private readonly ICollectionService _collectionService;
    private readonly IFolderService _folderService;
    private readonly ILogger<ExplorerWindowViewModel>? _logger;
    private readonly IMessenger _messenger;
    private readonly IPowerPasteService _powerPasteService;
    private readonly IQuickPasteService _quickPasteService;
    private readonly ISearchService _searchService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ITemplateService _templateService;

    public ExplorerWindowViewModel(CollectionTreeViewModel collectionTreeViewModel,
        ClipListViewModel clipListViewModel,
        PreviewPaneViewModel previewPaneViewModel,
        SearchViewModel searchViewModel,
        QuickPasteToolbarViewModel quickPasteToolbarViewModel,
        MainMenuViewModel mainMenuViewModel,
        IServiceProvider serviceProvider,
        IQuickPasteService quickPasteService,
        IPowerPasteService powerPasteService,
        ICollectionService collectionService,
        IFolderService folderService,
        ITemplateService templateService,
        ISearchService searchService,
        IMessenger messenger,
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
        _powerPasteService = powerPasteService ?? throw new ArgumentNullException(nameof(powerPasteService));
        _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
        _folderService = folderService ?? throw new ArgumentNullException(nameof(folderService));
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
        _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _logger = logger;

        // Subscribe to clip collection changes to update status bar statistics
        PrimaryClipList.Clips.CollectionChanged += OnClipsCollectionChanged;
        PrimaryClipList.PropertyChanged += OnClipListPropertyChanged;
        CollectionTree.PropertyChanged += OnCollectionTreePropertyChanged;

        // Subscribe to messenger events for status bar updates
        _messenger.Register<ClipSelectedEvent>(this);
        _messenger.Register<ImageDimensionsLoadedEvent>(this);
        _messenger.Register<ClipboardCopiedEvent>(this);
        _messenger.Register<DeleteClipsRequestedEvent>(this);
        _messenger.Register<RenameClipRequestedEvent>(this);
        _messenger.Register<CopyToCollectionRequestedEvent>(this);
        _messenger.Register<MoveToCollectionRequestedEvent>(this);
        _messenger.Register<CreateNewClipRequestedEvent>(this);
        _messenger.Register<PowerPasteUpRequestedEvent>(this);
        _messenger.Register<PowerPasteDownRequestedEvent>(this);
        _messenger.Register<PowerPasteToggleRequestedEvent>(this);
        _messenger.Register<ShowSearchWindowEvent>(this);
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
                    // Find the database key by traversing up from target node
                    var databaseKey = GetDatabaseKeyForNode(targetCollection);
                    if (string.IsNullOrEmpty(databaseKey))
                        _logger?.LogWarning("Could not determine database key for target collection");
                    else
                        await _collectionService.SetActiveAsync(targetCollection.Collection.Id, databaseKey);

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

    /// <summary>
    /// Sets the loading state with progress for status bar display.
    /// </summary>
    /// <param name="isLoading">Whether clips are currently loading.</param>
    /// <param name="progress">Loading progress percentage (0-100).</param>
    public void SetLoading(bool isLoading, int progress = 0)
    {
        IsLoading = isLoading;
        LoadingProgress = progress;
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
            // Brief delay to ensure the new foreground window is fully activated
            await Task.Delay(50);
            _quickPasteService.UpdateTarget();
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

    #region Status Bar Statistics

    /// <summary>
    /// Handles changes to the clip list collection to update status bar statistics.
    /// </summary>
    private void OnClipsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Clear selected clip reference when collection changes (new collection loaded)
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            _selectedClip = null;
            _clipboardCopied = false;
        }

        UpdateStatusBarStatistics(_selectedClip);
    }

    /// <summary>
    /// Handles property changes from ClipListViewModel to update status message.
    /// </summary>
    private void OnClipListPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ClipListViewModel.CurrentCollectionId) or nameof(ClipListViewModel.CurrentFolderId))
        {
            // Clear selected clip when collection/folder changes
            _selectedClip = null;
            _clipboardCopied = false;
            UpdateStatusMessage(_selectedClip);
        }
        else if (e.PropertyName == nameof(ClipListViewModel.IsLoading))
        {
            // Mirror the loading state from ClipListViewModel
            IsLoading = PrimaryClipList.IsLoading;

            // Show indeterminate progress (50%) when loading
            if (IsLoading)
                LoadingProgress = 50;
            else
                LoadingProgress = 0; // Clear progress when done
        }
    }

    /// <summary>
    /// Handles property changes from CollectionTreeViewModel to update status message.
    /// </summary>
    private void OnCollectionTreePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CollectionTreeViewModel.SelectedNode))
            UpdateStatusMessage(_selectedClip);
    }

    /// <summary>
    /// Updates status bar statistics (bytes, characters, words) based on loaded clips.
    /// </summary>
    /// <param name="selectedClip">The currently selected clip, or null to show collection totals.</param>
    private void UpdateStatusBarStatistics(Clip? selectedClip)
    {
        long bytes = 0;
        long chars = 0;
        long words = 0;

        if (selectedClip is { Type: ClipType.Text })
        {
            // Show statistics for the selected text clip
            bytes = selectedClip.Size;

            // If TextContent is loaded, use it for accurate counts
            if (!string.IsNullOrEmpty(selectedClip.TextContent))
            {
                chars = selectedClip.TextContent.Length;
                words = CountWords(selectedClip.TextContent);
            }
            else
            {
                // Estimate from Size (Unicode text = 2 bytes per char)
                chars = selectedClip.Size / 2;
                words = EstimateWords(chars);
            }
        }
        else if (selectedClip is not { Type: ClipType.Image })
        {
            // Show collection totals when no clip is selected or non-text/non-image clip is selected
            var clips = PrimaryClipList.Clips;

            foreach (var clip in clips)
            {
                bytes += clip.Size;

                if (clip.Type != ClipType.Text)
                    continue;

                // If TextContent is loaded, use it for accurate counts
                if (!string.IsNullOrEmpty(clip.TextContent))
                {
                    chars += clip.TextContent.Length;
                    words += CountWords(clip.TextContent);
                }
                else
                {
                    // Estimate from Size (Unicode text = 2 bytes per char)
                    var estimatedChars = clip.Size / 2;
                    chars += estimatedChars;
                    words += EstimateWords(estimatedChars);
                }
            }
        }

        TotalBytes = bytes;
        TotalChars = chars;
        TotalWords = words;

        UpdateStatusMessage(selectedClip);
    }

    /// <summary>
    /// Updates the status message based on the current collection/folder selection and selected clip.
    /// </summary>
    /// <param name="selectedClip">The currently selected clip, or null to show collection info.</param>
    private void UpdateStatusMessage(Clip? selectedClip)
    {
        var clipCount = PrimaryClipList.Clips.Count;

        // Empty state: no clips in collection and no clip selected
        if (clipCount == 0 && selectedClip == null)
        {
            StatusMessage = "ℹ There is nothing to display. Copy some data from an application, or select an existing clip.";
            return;
        }

        // Clip selected: show clip title with clipboard status
        if (selectedClip != null)
        {
            var clipTitle = selectedClip.Type == ClipType.Image
                ? $"Graphic: {selectedClip.CapturedAt:g}"
                : selectedClip.Title ?? "Untitled Clip";

            var copiedSuffix = _clipboardCopied
                ? " Copied to the System Clipboard."
                : string.Empty;

            StatusMessage = $"📋Clip Item [{clipTitle}]{copiedSuffix}";
            return;
        }

        // Collection loaded: show collection information
        var selectedNode = CollectionTree.SelectedNode;
        var containerName = "Items";

        if (selectedNode is CollectionTreeNode collectionNode)
            containerName = collectionNode.Name;
        else if (selectedNode is FolderTreeNode folderNode)
            containerName = folderNode.Name;

        StatusMessage = $"ℹ Loaded - {clipCount} Items from [{containerName}]";
    }

    /// <summary>
    /// Counts words in text (whitespace-separated tokens).
    /// </summary>
    private static long CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        return text.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries).LongLength;
    }

    /// <summary>
    /// Estimates word count from character count (average 5 chars per word + space).
    /// </summary>
    private static long EstimateWords(long charCount)
    {
        if (charCount == 0)
            return 0;

        // Average English word is ~5 characters + 1 space = 6 characters per word
        return charCount / 6;
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

    /// <summary>
    /// Loading progress percentage (0-100). Visible when IsLoading is true.
    /// </summary>
    [ObservableProperty]
    private int _loadingProgress;

    /// <summary>
    /// Indicates whether clips are currently being loaded (shows progress bar).
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Total bytes across all loaded clips.
    /// </summary>
    [ObservableProperty]
    private long _totalBytes;

    /// <summary>
    /// Total character count across all loaded clips.
    /// </summary>
    [ObservableProperty]
    private long _totalChars;

    /// <summary>
    /// Total word count across all loaded clips.
    /// </summary>
    [ObservableProperty]
    private long _totalWords;

    /// <summary>
    /// Width of the currently selected image clip in pixels.
    /// </summary>
    [ObservableProperty]
    private int _imageWidth;

    /// <summary>
    /// Height of the currently selected image clip in pixels.
    /// </summary>
    [ObservableProperty]
    private int _imageHeight;

    /// <summary>
    /// Indicates whether an image clip is currently selected.
    /// </summary>
    [ObservableProperty]
    private bool _isImageSelected;

    /// <summary>
    /// Indicates whether image dimensions are being loaded for the selected clip.
    /// </summary>
    [ObservableProperty]
    private bool _isLoadingImage;

    /// <summary>
    /// Currently selected clip (cached for status bar updates).
    /// </summary>
    private Clip? _selectedClip;

    /// <summary>
    /// The database key for the currently selected clip.
    /// </summary>
    private string? _selectedClipDatabaseKey;

    /// <summary>
    /// Tracks whether the selected clip has been copied to the clipboard.
    /// </summary>
    private bool _clipboardCopied;

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

            // Start PowerPaste
            var powerPasteDirection = direction == "Up"
                ? Core.Services.PowerPasteDirection.Up
                : Core.Services.PowerPasteDirection.Down;

            await _powerPasteService.StartAsync(
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

    #region Messenger Event Handlers

    /// <summary>
    /// Handles ClipSelectedEvent to update status bar when a clip is selected.
    /// </summary>
    public void Receive(ClipSelectedEvent message)
    {
        _selectedClip = message.SelectedClip;
        _selectedClipDatabaseKey = message.DatabaseKey;
        _clipboardCopied = false; // Reset clipboard status when clip changes

        // Reset image-related state
        IsImageSelected = _selectedClip?.Type == ClipType.Image;
        IsLoadingImage = IsImageSelected; // Show "Loading..." until dimensions arrive
        ImageWidth = 0;
        ImageHeight = 0;

        // Update statistics and message for the selected clip
        UpdateStatusBarStatistics(_selectedClip);
    }

    /// <summary>
    /// Handles ImageDimensionsLoadedEvent to update status bar when image dimensions are loaded.
    /// </summary>
    public void Receive(ImageDimensionsLoadedEvent message)
    {
        // Only update if this is for the currently selected clip
        if (_selectedClip?.Id != message.ClipId)
            return;

        ImageWidth = message.Width;
        ImageHeight = message.Height;
        IsLoadingImage = false;
    }

    /// <summary>
    /// Handles ClipboardCopiedEvent to update status bar when a clip is copied to the clipboard.
    /// </summary>
    public void Receive(ClipboardCopiedEvent message)
    {
        // Only update if this is the currently selected clip
        if (_selectedClip?.Id != message.Clip.Id)
            return;

        _clipboardCopied = true;
        UpdateStatusMessage(_selectedClip);
    }

    /// <summary>
    /// Handles ShortcutModeStatusMessage to update status bar during shortcut filtering.
    /// </summary>
    public void Receive(ShortcutModeStatusMessage message)
    {
        if (message.IsActive)
        {
            // Show shortcut mode status with filter and match count
            StatusMessage = $"({message.MatchCount}) All matches on '{message.Filter}' are retrieved and displayed.";
        }
        else
        {
            // Restore normal status message
            UpdateStatusMessage(_selectedClip);
        }
    }

    /// <summary>
    /// Handles DeleteClipsRequestedEvent to delete selected clips with confirmation.
    /// </summary>
    public async void Receive(DeleteClipsRequestedEvent message)
    {
        var selectedClips = PrimaryClipList.SelectedClips;
        if (selectedClips.Count == 0)
        {
            SetStatus("No clips selected");
            return;
        }

        // Show confirmation dialog
        var clipCount = selectedClips.Count;
        var confirmMessage = clipCount == 1
            ? $"Delete '{selectedClips[0].Title}'?"
            : $"Delete {clipCount} clips?";

        var result = DXMessageBox.Show(
            confirmMessage,
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            // Get database key from current collection tree node
            var databaseKey = GetDatabaseKeyForNode(CollectionTree.SelectedNode);
            if (string.IsNullOrEmpty(databaseKey))
            {
                _logger?.LogError("Cannot delete clips: database key not found");
                SetStatus("Error: database not found");
                return;
            }

            var clipService = _serviceProvider.GetRequiredService<IClipService>();

            foreach (var item in selectedClips)
                await clipService.DeleteAsync(databaseKey, item.Id);

            SetStatus($"Deleted {clipCount} clip(s)");

            // Reload the current collection
            await PrimaryClipList.LoadClipsAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to delete clips");
            SetStatus("Error deleting clips");
        }
    }

    /// <summary>
    /// Handles RenameClipRequestedEvent to rename a clip with a dialog.
    /// </summary>
    public async void Receive(RenameClipRequestedEvent message)
    {
        var selectedClip = PrimaryClipList.SelectedClip;
        if (selectedClip == null)
        {
            SetStatus("No clip selected");
            return;
        }

        // Get database key from current collection tree node
        var databaseKey = GetDatabaseKeyForNode(CollectionTree.SelectedNode);
        if (string.IsNullOrEmpty(databaseKey))
        {
            _logger?.LogError("Cannot rename clip: database key not found");
            SetStatus("Error: database not found");
            return;
        }

        try
        {
            // Get the RenameClipDialogViewModel from DI
            var viewModel = _serviceProvider.GetService<RenameClipDialogViewModel>();
            if (viewModel == null)
            {
                _logger?.LogError("RenameClipDialogViewModel not found in DI container");
                SetStatus("Error: dialog service not available");
                return;
            }

            // Get existing shortcut if any
            var shortcutService = _serviceProvider.GetService<IShortcutService>();
            Shortcut? existingShortcut = null;
            if (shortcutService != null)
            {
                try
                {
                    existingShortcut = await shortcutService.GetByClipIdAsync(databaseKey, selectedClip.Id);
                }
                catch (Exception ex) when (ex.Message.Contains("no such table"))
                {
                    // ShortCut table doesn't exist yet - this is OK
                    // The table will be created automatically when first shortcut is saved
                    _logger?.LogDebug("ShortCut table not found - will be created on first shortcut save");
                }
            }

            // Initialize the dialog ViewModel
            await viewModel.InitializeAsync(
                selectedClip.Id,
                databaseKey,
                selectedClip.Title,
                existingShortcut?.Nickname);

            // Create and show the dialog
            var dialog = new RenameClipDialog
            {
                DataContext = viewModel,
                Owner = Application.Current.GetDialogOwner(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };

            if (dialog.ShowDialog() == true)
            {
                // Update the clip in the collection
                var clip = PrimaryClipList.Clips.FirstOrDefault(p => p.Id == selectedClip.Id);
                clip?.Title = viewModel.Title;

                var title = viewModel.Title ?? string.Empty;
                SetStatus($"Updated clip: {title}");

                // Message will be sent by the ViewModel, which will trigger grid refresh
            }
            else
                SetStatus("Rename cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to show rename dialog");
            SetStatus("Error showing rename dialog");
        }
    }

    /// <summary>
    /// Handles CopyToCollectionRequestedEvent to copy clips to another collection.
    /// Supports both same-database and cross-database operations.
    /// </summary>
    public async void Receive(CopyToCollectionRequestedEvent message)
    {
        var selectedClips = PrimaryClipList.SelectedClips;
        if (selectedClips.Count == 0)
        {
            SetStatus("No clips selected");
            return;
        }

        // Get source database key
        var sourceDatabaseKey = GetDatabaseKeyForNode(CollectionTree.SelectedNode);
        if (string.IsNullOrEmpty(sourceDatabaseKey))
        {
            _logger?.LogError("Cannot copy clips: source database key not found");
            SetStatus("Error: source database not found");
            return;
        }

        // Create and show collection picker dialog
        var dialog = new CollectionPickerDialog(_serviceProvider)
        {
            Message = $"Select a collection to copy {selectedClips.Count} clip(s) to:",
            Owner = Application.Current.GetDialogOwner(),
        };

        await dialog.LoadCollectionsAsync();

        if (dialog.ShowDialog() != true || dialog.SelectedCollectionId is null || string.IsNullOrEmpty(dialog.SelectedDatabaseKey))
        {
            SetStatus("Copy cancelled");
            return;
        }

        try
        {
            var clipService = _serviceProvider.GetRequiredService<IClipService>();
            var targetDatabaseKey = dialog.SelectedDatabaseKey;
            var isCrossDatabase = !sourceDatabaseKey.Equals(targetDatabaseKey, StringComparison.OrdinalIgnoreCase);

            var copiedCount = 0;
            foreach (var item in selectedClips)
            {
                if (isCrossDatabase)
                {
                    // Cross-database copy
                    await clipService.CopyClipCrossDatabaseAsync(
                        sourceDatabaseKey,
                        item.Id,
                        targetDatabaseKey,
                        dialog.SelectedCollectionId.Value);
                }
                else
                {
                    // Same-database copy
                    await clipService.CopyClipAsync(
                        sourceDatabaseKey,
                        item.Id,
                        dialog.SelectedCollectionId.Value);
                }

                copiedCount++;
            }

            var databaseMessage = isCrossDatabase
                ? " (cross-database)"
                : string.Empty;

            SetStatus($"Copied {copiedCount} clip(s){databaseMessage}");
            await PrimaryClipList.LoadClipsAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to copy clips");
            SetStatus("Error copying clips");
        }
    }

    /// <summary>
    /// Handles MoveToCollectionRequestedEvent to move clips to another collection.
    /// Supports both same-database and cross-database operations.
    /// </summary>
    public async void Receive(MoveToCollectionRequestedEvent message)
    {
        var selectedClips = PrimaryClipList.SelectedClips;
        if (selectedClips.Count == 0)
        {
            SetStatus("No clips selected");
            return;
        }

        // Get source database key
        var sourceDatabaseKey = GetDatabaseKeyForNode(CollectionTree.SelectedNode);
        if (string.IsNullOrEmpty(sourceDatabaseKey))
        {
            _logger?.LogError("Cannot move clips: source database key not found");
            SetStatus("Error: source database not found");
            return;
        }

        // Create and show collection picker dialog
        var dialog = new CollectionPickerDialog(_serviceProvider)
        {
            Message = $"Select a collection to move {selectedClips.Count} clip(s) to:",
            Owner = Application.Current.GetDialogOwner(),
        };

        await dialog.LoadCollectionsAsync();

        if (dialog.ShowDialog() != true || dialog.SelectedCollectionId is null || string.IsNullOrEmpty(dialog.SelectedDatabaseKey))
        {
            SetStatus("Move cancelled");
            return;
        }

        try
        {
            var clipService = _serviceProvider.GetRequiredService<IClipService>();
            var targetDatabaseKey = dialog.SelectedDatabaseKey;
            var isCrossDatabase = !sourceDatabaseKey.Equals(targetDatabaseKey, StringComparison.OrdinalIgnoreCase);

            var movedCount = 0;
            foreach (var item in selectedClips)
            {
                if (isCrossDatabase)
                {
                    // Cross-database move (copy + delete)
                    await clipService.MoveClipCrossDatabaseAsync(
                        sourceDatabaseKey,
                        item.Id,
                        targetDatabaseKey,
                        dialog.SelectedCollectionId.Value);
                }
                else
                {
                    // Same-database move (update CollectionId)
                    await clipService.MoveClipAsync(
                        sourceDatabaseKey,
                        item.Id,
                        dialog.SelectedCollectionId.Value);
                }

                movedCount++;
            }

            var databaseMessage = isCrossDatabase
                ? " (cross-database)"
                : string.Empty;

            SetStatus($"Moved {movedCount} clip(s){databaseMessage}");
            await PrimaryClipList.LoadClipsAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to move clips");
            SetStatus("Error moving clips");
        }
    }

    /// <summary>
    /// Handles CreateNewClipRequestedEvent to create a new empty clip in the current collection.
    /// </summary>
    public async void Receive(CreateNewClipRequestedEvent message)
    {
        var currentCollectionId = PrimaryClipList.CurrentCollectionId;
        if (currentCollectionId == null)
        {
            SetStatus("No collection selected");
            return;
        }

        // Get database key from current collection tree node
        var databaseKey = GetDatabaseKeyForNode(CollectionTree.SelectedNode);
        if (string.IsNullOrEmpty(databaseKey))
        {
            _logger?.LogError("Cannot create clip: database key not found");
            SetStatus("Error: database not found");
            return;
        }

        try
        {
            var clipService = _serviceProvider.GetRequiredService<IClipService>();

            // Create new empty clip
            var newClip = new Clip
            {
                Title = "New Clip",
                TextContent = string.Empty,
                CollectionId = currentCollectionId.Value,
                CapturedAt = DateTimeOffset.UtcNow,
                Type = ClipType.Text,
            };

            var createdClip = await clipService.CreateAsync(databaseKey, newClip);

            SetStatus($"Created new clip: {createdClip.Title}");

            // Reload to show the new clip
            await PrimaryClipList.LoadClipsAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create new clip");
            SetStatus("Error creating new clip");
        }
    }

    /// <summary>
    /// Handles PowerPasteUpRequestedEvent to start PowerPaste in upward direction.
    /// </summary>
    public async void Receive(PowerPasteUpRequestedEvent message)
    {
        var selectedClips = PrimaryClipList.SelectedClips;
        if (selectedClips.Count == 0)
        {
            SetStatus("No clips selected for PowerPaste");
            return;
        }

        try
        {
            await _powerPasteService.StartAsync(selectedClips, Core.Services.PowerPasteDirection.Up);
            SetStatus($"PowerPaste Up started with {selectedClips.Count} clip(s)");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start PowerPaste Up");
            SetStatus("Error starting PowerPaste");
        }
    }

    /// <summary>
    /// Handles PowerPasteDownRequestedEvent to start PowerPaste in downward direction.
    /// </summary>
    public async void Receive(PowerPasteDownRequestedEvent message)
    {
        var selectedClips = PrimaryClipList.SelectedClips;
        if (selectedClips.Count == 0)
        {
            SetStatus("No clips selected for PowerPaste");
            return;
        }

        try
        {
            await _powerPasteService.StartAsync(selectedClips, Core.Services.PowerPasteDirection.Down);
            SetStatus($"PowerPaste Down started with {selectedClips.Count} clip(s)");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start PowerPaste Down");
            SetStatus("Error starting PowerPaste");
        }
    }

    /// <summary>
    /// Handles PowerPasteToggleRequestedEvent to toggle PowerPaste state.
    /// </summary>
    public async void Receive(PowerPasteToggleRequestedEvent message)
    {
        // If PowerPaste is active, stop it
        if (_powerPasteService.State == PowerPasteState.Active)
        {
            _powerPasteService.Stop();
            SetStatus("PowerPaste stopped");
            return;
        }

        // Otherwise, start it in the default direction (Down)
        var selectedClips = PrimaryClipList.SelectedClips;
        if (selectedClips.Count == 0)
        {
            SetStatus("No clips selected for PowerPaste");
            return;
        }

        try
        {
            await _powerPasteService.StartAsync(selectedClips, Core.Services.PowerPasteDirection.Down);
            SetStatus($"PowerPaste started with {selectedClips.Count} clip(s)");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to toggle PowerPaste");
            SetStatus("Error starting PowerPaste");
        }
    }

    /// <summary>
    /// Selects a template for clip merging.
    /// If a clip is currently selected, immediately applies the template to it.
    /// </summary>
    /// <param name="templateName">Template name, or null for "No Template".</param>
    public async void SelectTemplate(string? templateName)
    {
        try
        {
            await _templateService.SetActiveTemplateAsync(templateName);

            var statusMessage = string.IsNullOrWhiteSpace(templateName)
                ? "Template cleared"
                : $"Template selected: {templateName}";

            SetStatus(statusMessage);

            // Note: Template will be applied on next clipboard operation
            // No need to re-trigger clipboard set here
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to select template: {TemplateName}", templateName);
            SetStatus("Error selecting template");
        }
    }

    /// <summary>
    /// Resets the template sequence counter to 1.
    /// </summary>
    public void ResetTemplateSequence()
    {
        _templateService.ResetSequenceCounter();
        SetStatus("Template sequence reset to 1");
    }

    /// <summary>
    /// Handles ShowSearchWindowEvent to display the search dialog.
    /// </summary>
    public void Receive(ShowSearchWindowEvent message)
    {
        _logger?.LogInformation("Showing search window");

        try
        {
            var logger = _serviceProvider.GetRequiredService<ILogger<SearchDialog>>();
            var dialog = new SearchDialog(Search, _searchService, _collectionService, logger)
            {
                Owner = Application.Current.GetDialogOwner(),
            };

            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to show search window");
            SetStatus("Error showing search window");
        }
    }

    #endregion
}
