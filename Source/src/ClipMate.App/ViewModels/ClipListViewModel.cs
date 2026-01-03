using System.Collections.ObjectModel;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using ClipMate.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using ThrottleDebounce;
using Application = System.Windows.Application;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the clip list pane (middle pane).
/// Displays clips in list or grid view, handles selection, and loading.
/// Implements IRecipient to receive ClipAddedEvent and CollectionNodeSelectedEvent messages via MVVM Toolkit
/// Messenger.
/// </summary>
public partial class ClipListViewModel : ObservableObject,
    IRecipient<ClipAddedEvent>,
    IRecipient<CollectionNodeSelectedEvent>,
    IRecipient<SearchResultsSelectedEvent>,
    IRecipient<QuickPasteNowEvent>,
    IRecipient<SelectNextClipEvent>,
    IRecipient<SelectPreviousClipEvent>
{
    private readonly IClipService _clipService;
    private readonly ICollectionService _collectionService;
    private readonly IDatabaseContextFactory _databaseContextFactory;
    private readonly RateLimitedAction _debouncedClipSelection;
    private readonly IFolderService _folderService;
    private readonly ILogger<ClipListViewModel> _logger;
    private readonly IMessenger _messenger;
    private readonly IQuickPasteService _quickPasteService;

    /// <summary>
    /// Indicates whether the current view accepts new clips from clipboard capture.
    /// False for virtual collections (search results, deleted clips, etc.) that show fixed/filtered content.
    /// </summary>
    [ObservableProperty]
    private bool _acceptsNewClips = true;

    [ObservableProperty]
    private ObservableCollection<Clip> _clips = [];

    [ObservableProperty]
    private Guid? _currentCollectionId;

    [ObservableProperty]
    private string? _currentDatabaseKey;

    [ObservableProperty]
    private Guid? _currentFolderId;

    [ObservableProperty]
    private bool _isGridView;

    [ObservableProperty]
    private bool _isListView = true;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private Clip? _selectedClip;

    [ObservableProperty]
    private ObservableCollection<Clip> _selectedClips = [];

    private CancellationTokenSource? _setClipboardCts;

    /// <summary>
    /// Collection of clips representing shortcuts (displayed in shortcuts grid during shortcut mode).
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<Clip> _shortcutClips = [];

    public ClipListViewModel(ICollectionService collectionService,
        IFolderService folderService,
        IClipService clipService,
        IQuickPasteService quickPasteService,
        IDatabaseContextFactory databaseContextFactory,
        IMessenger messenger,
        ILogger<ClipListViewModel> logger)
    {
        _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
        _folderService = folderService ?? throw new ArgumentNullException(nameof(folderService));
        _clipService = clipService ?? throw new ArgumentNullException(nameof(clipService));
        _quickPasteService = quickPasteService ?? throw new ArgumentNullException(nameof(quickPasteService));
        _databaseContextFactory = databaseContextFactory ?? throw new ArgumentNullException(nameof(databaseContextFactory));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize debouncer with 400ms delay to prevent clipboard contention during rapid navigation
        // Increased from 150ms to allow clipboard operations to complete before next selection
        _debouncedClipSelection = Debouncer.Debounce(ProcessClipSelection, TimeSpan.FromMilliseconds(400));

        // Register this ViewModel as a recipient for clipboard and selection events
        // The messenger will automatically handle weak references and cleanup
        _messenger.Register<ClipAddedEvent>(this);
        _messenger.Register<CollectionNodeSelectedEvent>(this);
        _messenger.Register<QuickPasteNowEvent>(this);
        _messenger.Register<SelectNextClipEvent>(this);
        _messenger.Register<SelectPreviousClipEvent>(this);
        _messenger.Register<SearchResultsSelectedEvent>(this);
    }

    /// <summary>
    /// Receives ClipAddedEvent messages from the messenger.
    /// This method is called automatically by the messenger when a ClipAddedEvent is sent.
    /// </summary>
    public void Receive(ClipAddedEvent message)
    {
        // Ensure we're on the UI thread
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            // Set the database key if not already set (handles clips arriving before collection selection)
            if (string.IsNullOrEmpty(CurrentDatabaseKey))
            {
                CurrentDatabaseKey = message.DatabaseKey;
                _logger.LogInformation("[ClipListViewModel] CurrentDatabaseKey set from ClipAddedEvent: {DatabaseKey}", message.DatabaseKey);
            }

            // Check if this clip should be displayed in the current view
            var shouldDisplay = ShouldDisplayClip(message.Clip, message.CollectionId, message.FolderId);

            if (!shouldDisplay)
                return;

            // Check if clip already exists in the collection (duplicate handling)
            var existingClip = Clips.FirstOrDefault(p => p.Id == message.Clip.Id);

            if (existingClip != null)
            {
                // Duplicate - update existing clip's timestamp to bring it to the top
                // Remove and re-add at the top
                Clips.Remove(existingClip);
                Clips.Insert(0, message.Clip);
            }
            else
            {
                // New clip - add to the top of the list
                Clips.Insert(0, message.Clip);

                // Optional: Limit list size to prevent memory issues
                const int maxClips = 10000;
                while (Clips.Count > maxClips)
                    Clips.RemoveAt(Clips.Count - 1);
            }

            // Auto-select the new clip
            SelectedClip = message.Clip;
        });
    }

    /// <summary>
    /// Receives CollectionNodeSelectedEvent messages from the messenger.
    /// Loads clips for the selected collection/folder.
    /// </summary>
    public async void Receive(CollectionNodeSelectedEvent message)
    {
        _logger.LogInformation("CollectionNodeSelectedEvent received: DatabaseKey={DatabaseKey}, CollectionId={CollectionId}, FolderId={FolderId}, IsTrashcan={IsTrashcan}",
            message.DatabaseKey, message.CollectionId, message.FolderId, message.IsTrashcan);

        // Store the current database key
        CurrentDatabaseKey = message.DatabaseKey;

        // Set the active collection and folder for new clipboard captures
        // Skip this for virtual collections like Trashcan (they don't receive new clips)
        if (!message.IsTrashcan)
        {
            await _collectionService.SetActiveAsync(message.CollectionId, message.DatabaseKey);
            await _folderService.SetActiveAsync(message.FolderId);

            _logger.LogInformation("Active collection and folder updated for new clipboard captures");
        }

        // Load clips for the selected node
        if (message.IsTrashcan)
        {
            // Load deleted clips for the Trashcan virtual collection
            await LoadDeletedClipsAsync(message.DatabaseKey);
        }
        else if (message.FolderId.HasValue)
        {
            // Load clips for the selected folder
            await LoadClipsByFolderAsync(message.CollectionId, message.FolderId.Value);
        }
        else
        {
            // Load clips for the selected collection
            await LoadClipsByCollectionAsync(message.CollectionId);
        }
    }

    /// <summary>
    /// Receives QuickPasteNowEvent messages from the QuickPaste toolbar.
    /// This pastes the currently selected clip using QuickPaste.
    /// </summary>
    public async void Receive(QuickPasteNowEvent message)
    {
        if (SelectedClip == null)
        {
            _logger.LogWarning("QuickPasteNow triggered but no clip is selected");

            return;
        }

        _logger.LogInformation("QuickPasteNow received, pasting clip: {ClipId}", SelectedClip.Id);

        try
        {
            // Paste the clip using QuickPaste
            await _quickPasteService.PasteClipAsync(SelectedClip);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to paste clip via QuickPaste: {ClipId}", SelectedClip.Id);
        }
    }

    /// <summary>
    /// Receives SearchResultsSelectedEvent when search results are selected in the collection tree.
    /// Loads the specific clips that match the search query.
    /// </summary>
    public async void Receive(SearchResultsSelectedEvent message)
    {
        _logger.LogInformation("SearchResultsSelectedEvent received: DatabaseKey={DatabaseKey}, Query={Query}, ClipCount={Count}",
            message.DatabaseKey, message.Query, message.ClipIds.Count);

        _logger.LogInformation("Clip IDs to load: {ClipIds}", string.Join(", ", message.ClipIds.Take(10)));

        // Store the current database key
        CurrentDatabaseKey = message.DatabaseKey;

        // Search results don't accept new clips
        AcceptsNewClips = false;

        // Load clips by the specific IDs from search results
        await LoadClipsByIdsAsync(message.ClipIds, message.DatabaseKey);
    }

    /// <summary>
    /// Handles SelectNextClipEvent by moving selection to the next clip in the list.
    /// </summary>
    public void Receive(SelectNextClipEvent message)
    {
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (Clips.Count == 0)
                return;

            var currentIndex = SelectedClip is not null
                ? Clips.IndexOf(SelectedClip)
                : -1;

            var nextIndex = (currentIndex + 1) % Clips.Count;
            SelectedClip = Clips[nextIndex];
        });
    }

    /// <summary>
    /// Handles SelectPreviousClipEvent by moving selection to the previous clip in the list.
    /// </summary>
    public void Receive(SelectPreviousClipEvent message)
    {
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (Clips.Count == 0)
                return;

            var currentIndex = SelectedClip is not null
                ? Clips.IndexOf(SelectedClip)
                : -1;

            var previousIndex = currentIndex <= 0
                ? Clips.Count - 1
                : currentIndex - 1;

            SelectedClip = Clips[previousIndex];
        });
    }

    partial void OnSelectedClipChanged(Clip? value)
    {
        _logger.LogInformation("[ClipListViewModel] SelectedClip changed to: {ClipId}, Title: {Title}, DatabaseKey: {DatabaseKey}",
            value?.Id, value?.DisplayTitle, CurrentDatabaseKey);

        // Debounce clip selection to only process the final clip after navigation stops
        // This prevents UI flickering and wasted operations during rapid arrow key navigation
        _debouncedClipSelection.Invoke();
    }

    /// <summary>
    /// Processes the debounced clip selection.
    /// Called only after selection has stabilized (150ms with no further changes).
    /// Executed on a background thread by the debouncer, so marshals back to UI thread.
    /// </summary>
    private void ProcessClipSelection()
    {
        // Debouncer executes on background thread, so marshal to UI thread
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var clip = SelectedClip;
            if (clip == null)
                return;

            // Cancel previous clipboard operation if still running
            _setClipboardCts?.Cancel();
            _setClipboardCts?.Dispose();
            _setClipboardCts = null;

            // Send messenger event when selection changes
            _messenger.Send(new ClipSelectedEvent(clip, CurrentDatabaseKey));

            // Automatically load the selected clip onto the system clipboard
            // This is ClipMate's standard "Pick, Flip, and Paste" behavior
            _setClipboardCts = new CancellationTokenSource();
            _ = SetClipboardContentAsync(clip, _setClipboardCts.Token);
        });
    }

    /// <summary>
    /// Loads the clip's full content from the database and sets it to the Windows clipboard.
    /// Called automatically when a clip is selected (Pick, Flip, and Paste), or manually via double-click/Enter/context menu.
    /// </summary>
    public async Task SetClipboardContentAsync(Clip clip, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get database context for the current database
            if (string.IsNullOrEmpty(CurrentDatabaseKey))
                throw new InvalidOperationException("No database is currently selected");

            // Use the centralized service method to load and set clipboard
            await _clipService.LoadAndSetClipboardAsync(CurrentDatabaseKey, clip.Id, cancellationToken);

            _logger.LogInformation("[ClipListViewModel] Set clipboard content for clip: {ClipId}", clip.Id);

            // Notify that clipboard has been updated
            _messenger.Send(new ClipboardCopiedEvent(clip));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ClipListViewModel] Failed to set clipboard content for clip: {ClipId}", clip.Id);
        }
    }

    /// <summary>
    /// Determines if a clip should be displayed in the current view based on active filters.
    /// </summary>
    private bool ShouldDisplayClip(Clip clip, Guid? clipCollectionId, Guid? clipFolderId)
    {
        // Views that don't accept new clips (search results, trash, etc.) should reject them
        if (!AcceptsNewClips)
            return false;

        // If viewing a specific folder, only show clips in that folder
        if (CurrentFolderId.HasValue && CurrentCollectionId.HasValue)
            return clipFolderId == CurrentFolderId && clipCollectionId == CurrentCollectionId;

        // If viewing a specific collection, only show clips in that collection
        if (CurrentCollectionId.HasValue)
            return clipCollectionId == CurrentCollectionId;

        // Default view - show all clips (or implement your default logic)
        return true;
    }

    /// <summary>
    /// Loads clips from the clip service.
    /// </summary>
    /// <param name="count">Maximum number of clips to load (default 1000).</param>
    public async Task LoadClipsAsync(int count = 1000)
    {
        try
        {
            IsLoading = true;
            AcceptsNewClips = true; // Recent clips view accepts new clips
            _logger.LogInformation("Loading recent {Count} clips (no collection filter)", count);
            IReadOnlyCollection<Clip> clips;
            var activeDatabaseKey = _collectionService.GetActiveDatabaseKey();

            if (string.IsNullOrEmpty(activeDatabaseKey))
            {
                _logger.LogWarning("No active database key found, cannot load recent clips");
                clips = [];
            }
            else
                clips = await _clipService.GetRecentAsync(activeDatabaseKey, count);

            _logger.LogInformation("Retrieved {Count} recent clips. First clip CollectionId: {CollectionId}", clips.Count, clips.FirstOrDefault()?.CollectionId);

            // Update collection on UI thread
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Clips.Clear();
                foreach (var item in clips)
                    Clips.Add(item);

                _logger.LogInformation("Updated UI collection: {Count} clips now in Clips collection", Clips.Count);
                // Auto-select the first (most recent) clip
                SelectedClip = Clips.FirstOrDefault();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading recent clips");
            await Application.Current.Dispatcher.InvokeAsync(() => Clips.Clear());
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Loads clips for a specific collection.
    /// </summary>
    /// <param name="collectionId">The collection ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task LoadClipsByCollectionAsync(Guid collectionId, CancellationToken cancellationToken = default)
    {
        try
        {
            IsLoading = true;
            CurrentCollectionId = collectionId;
            CurrentFolderId = null;

            _logger.LogInformation("Loading clips for collection: {CollectionId}", collectionId);
            IReadOnlyCollection<Clip> clips;
            // Get the active database key
            var activeDatabaseKey = _collectionService.GetActiveDatabaseKey();
            if (string.IsNullOrEmpty(activeDatabaseKey))
            {
                _logger.LogError("No active database key found, cannot load clips");
                clips = [];
                AcceptsNewClips = true; // Default to accepting new clips
            }
            else
            {
                // Get the collection to check if it's a virtual collection with SQL query
                var collection = await _collectionService.GetByIdAsync(collectionId, cancellationToken);
                
                if (collection != null && collection.IsVirtual && !string.IsNullOrWhiteSpace(collection.Sql))
                {
                    // Virtual collection - execute SQL query
                    _logger.LogInformation("Executing SQL query for virtual collection: {CollectionName}", collection.Title);
                    AcceptsNewClips = false; // Virtual collections don't accept new clips
                    clips = await _clipService.ExecuteSqlQueryAsync(activeDatabaseKey, collection.Sql, collection.RetentionLimit, cancellationToken);
                    _logger.LogInformation("Retrieved {Count} clips from SQL query for virtual collection '{CollectionName}'", clips.Count, collection.Title);
                }
                else
                {
                    // Normal collection - load by collection ID
                    AcceptsNewClips = true; // Normal collection view accepts new clips
                    var clipRepository = _databaseContextFactory.GetClipRepository(activeDatabaseKey);
                    clips = await clipRepository.GetByCollectionAsync(collectionId, cancellationToken);
                    _logger.LogInformation("Retrieved {Count} clips from database '{DatabaseKey}'", clips.Count, activeDatabaseKey);
                }
            }

            // Update collection on UI thread
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Clips.Clear();
                var clipList = clips.ToList();
                foreach (var item in clipList)
                    Clips.Add(item);

                _logger.LogInformation("Updated UI collection: {Count} clips now in Clips collection", Clips.Count);
                // Auto-select the first (most recent) clip
                SelectedClip = Clips.FirstOrDefault();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading clips for collection: {CollectionId}", collectionId);
            await Application.Current.Dispatcher.InvokeAsync(() => Clips.Clear());
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Loads clips for a specific folder.
    /// </summary>
    /// <param name="collectionId">The collection ID (for tracking).</param>
    /// <param name="folderId">The folder ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task LoadClipsByFolderAsync(Guid collectionId, Guid folderId, CancellationToken cancellationToken = default)
    {
        try
        {
            IsLoading = true;
            CurrentCollectionId = collectionId;
            CurrentFolderId = folderId;
            AcceptsNewClips = true; // Normal folder view accepts new clips

            _logger.LogInformation("Loading clips for folder: {FolderId}", folderId);
            IReadOnlyCollection<Clip> clips;
            // Get the active database key
            var activeDatabaseKey = _collectionService.GetActiveDatabaseKey();
            if (string.IsNullOrEmpty(activeDatabaseKey))
            {
                _logger.LogError("No active database key found, cannot load clips");
                clips = [];
            }
            else
            {
                // Create repository using the factory
                var clipRepository = _databaseContextFactory.GetClipRepository(activeDatabaseKey);
                clips = await clipRepository.GetByFolderAsync(folderId, cancellationToken);
                _logger.LogInformation("Retrieved {Count} clips from database '{DatabaseKey}'", clips.Count, activeDatabaseKey);
            }

            // Update collection on UI thread
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Clips.Clear();
                foreach (var item in clips)
                    Clips.Add(item);

                _logger.LogInformation("Updated UI collection: {Count} clips now in Clips collection", Clips.Count);
                // Auto-select the first (most recent) clip
                SelectedClip = Clips.FirstOrDefault();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading clips for folder: {FolderId}", folderId);
            await Application.Current.Dispatcher.InvokeAsync(() => Clips.Clear());
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Loads deleted clips for the Trashcan virtual collection.
    /// </summary>
    /// <param name="databaseKey">The database key to load from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task LoadDeletedClipsAsync(string databaseKey, CancellationToken cancellationToken = default)
    {
        try
        {
            IsLoading = true;
            CurrentCollectionId = null;
            CurrentFolderId = null;
            AcceptsNewClips = false; // Trash doesn't accept new clips

            _logger.LogInformation("Loading deleted clips for database: {DatabaseKey}", databaseKey);

            // Create repository using the factory
            var clipRepository = _databaseContextFactory.GetClipRepository(databaseKey);
            IReadOnlyCollection<Clip> clips = await clipRepository.GetDeletedAsync(cancellationToken);
            _logger.LogInformation("Retrieved {Count} deleted clips from database '{DatabaseKey}'", clips.Count, databaseKey);

            // Update collection on UI thread
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Clips.Clear();
                foreach (var item in clips)
                    Clips.Add(item);

                _logger.LogInformation("Updated UI collection: {Count} deleted clips now in Clips collection", Clips.Count);
                // Auto-select the first (most recent) clip
                SelectedClip = Clips.FirstOrDefault();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading deleted clips for database: {DatabaseKey}", databaseKey);
            await Application.Current.Dispatcher.InvokeAsync(() => Clips.Clear());
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Loads specific clips by their IDs (used for search results).
    /// </summary>
    /// <param name="clipIds">The list of clip IDs to load.</param>
    /// <param name="databaseKey">The database key to load clips from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task LoadClipsByIdsAsync(IReadOnlyList<Guid> clipIds, string databaseKey, CancellationToken cancellationToken = default)
    {
        try
        {
            IsLoading = true;
            CurrentCollectionId = null;
            CurrentFolderId = null;
            AcceptsNewClips = false; // Loading by IDs (search results) doesn't accept new clips

            _logger.LogInformation("Loading {Count} clips by IDs for database: {DatabaseKey}", clipIds.Count, databaseKey);

            // Create repository using the factory
            var clipRepository = _databaseContextFactory.GetClipRepository(databaseKey);

            // Load each clip by ID
            var clips = new List<Clip>();
            foreach (var item in clipIds)
            {
                var clip = await clipRepository.GetByIdAsync(item, cancellationToken);
                if (clip != null)
                    clips.Add(clip);
            }

            _logger.LogInformation("Retrieved {Count} clips from database '{DatabaseKey}'", clips.Count, databaseKey);

            // Update collection on UI thread
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Clips.Clear();
                foreach (var item in clips)
                    Clips.Add(item);

                _logger.LogInformation("Updated UI collection: {Count} clips now in Clips collection", Clips.Count);
                // Auto-select the first (most recent) clip
                SelectedClip = Clips.FirstOrDefault();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading clips by IDs for database: {DatabaseKey}", databaseKey);
            await Application.Current.Dispatcher.InvokeAsync(() => Clips.Clear());
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Refreshes the clip list by reloading from the service.
    /// </summary>
    public async Task RefreshAsync()
    {
        if (CurrentFolderId.HasValue && CurrentCollectionId.HasValue)
            await LoadClipsByFolderAsync(CurrentCollectionId.Value, CurrentFolderId.Value);
        else if (CurrentCollectionId.HasValue)
            await LoadClipsByCollectionAsync(CurrentCollectionId.Value);
        else
            await LoadClipsAsync();
    }

    /// <summary>
    /// Switches the view to list mode.
    /// </summary>
    public void SwitchToListView()
    {
        IsListView = true;
        IsGridView = false;
    }

    /// <summary>
    /// Switches the view to grid mode.
    /// </summary>
    public void SwitchToGridView()
    {
        IsGridView = true;
        IsListView = false;
    }

    partial void OnIsListViewChanged(bool value)
    {
        if (value)
            IsGridView = false;
    }

    partial void OnIsGridViewChanged(bool value)
    {
        if (value)
            IsListView = false;
    }
}
