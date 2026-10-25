using System.Collections.ObjectModel;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using ClipMate.Data;
using ClipMate.Platform.Helpers;
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
    IRecipient<ClipContentUpdatedEvent>,
    IRecipient<ClipCacheExpiredMessage>,
    IRecipient<CollectionNodeSelectedEvent>,
    IRecipient<RemoteDatabaseNodeSelectedEvent>,
    IRecipient<RemoteSearchResultsSelectedEvent>,
    IRecipient<SearchResultsSelectedEvent>,
    IRecipient<QuickPasteNowEvent>,
    IRecipient<SelectNextClipEvent>,
    IRecipient<SelectPreviousClipEvent>,
    IRecipient<SelectClipRequestedEvent>,
    IRecipient<ShowAllClipsInChildrenRequestedEvent>,
    IRecipient<ClipsDeletedEvent>,
    IDisposable
{
    private readonly IClipService _clipService;
    private readonly ICollectionService _collectionService;
    private readonly IDatabaseContextFactory _databaseContextFactory;
    private readonly RateLimitedFunc<bool> _debouncedClipSelection;
    private readonly IFolderService _folderService;
    private readonly ILogger<ClipListViewModel> _logger;
    private readonly IMessenger _messenger;
    private readonly PowerPasteStatus _powerPasteStatus;
    private readonly IQuickPasteService _quickPasteService;
    private readonly IRemoteDatabaseService _remoteDatabaseService;
    private CancellationTokenSource? _remoteLoadCts;

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

    /// <summary>
    /// Whether the list shows clips of a Remote Database. Remote Databases are read-only: nothing in this view may
    /// modify, move, rename or delete their clips, and they never receive captures.
    /// </summary>
    [ObservableProperty]
    private bool _isRemoteDatabase;

    /// <summary>
    /// Progress while a Remote Database's clips load page by page, or why they could not be loaded (Peer offline,
    /// timeout); <c>null</c> when there is nothing to report.
    /// </summary>
    [ObservableProperty]
    private string? _remoteStatusMessage;

    private Clip? _previousSelectedClip; // Track previous clip to reset IsDecrypted flag

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

    /// <summary>
    /// The clip whose selection must not copy it to the clipboard: one just captured (the source application's own,
    /// complete data is already there) or one selected while a list loads. Kept until a different clip is selected,
    /// because selection is handled after a debounce and may be reported more than once. A flag reset right after
    /// setting <see cref="SelectedClip" /> was always clear by then, so every capture replaced the clipboard.
    /// </summary>
    private Guid? _clipNotToCopyOnSelection;

    public ClipListViewModel(ICollectionService collectionService,
        IFolderService folderService,
        IClipService clipService,
        IQuickPasteService quickPasteService,
        PowerPasteStatus powerPasteStatus,
        IDatabaseContextFactory databaseContextFactory,
        IRemoteDatabaseService remoteDatabaseService,
        IMessenger messenger,
        ILogger<ClipListViewModel> logger)
    {
        _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
        _folderService = folderService ?? throw new ArgumentNullException(nameof(folderService));
        _clipService = clipService ?? throw new ArgumentNullException(nameof(clipService));
        _quickPasteService = quickPasteService ?? throw new ArgumentNullException(nameof(quickPasteService));
        _powerPasteStatus = powerPasteStatus ?? throw new ArgumentNullException(nameof(powerPasteStatus));
        _databaseContextFactory = databaseContextFactory ?? throw new ArgumentNullException(nameof(databaseContextFactory));
        _remoteDatabaseService = remoteDatabaseService ?? throw new ArgumentNullException(nameof(remoteDatabaseService));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize debouncer with 400ms delay to prevent clipboard contention during rapid navigation
        // Increased from 150ms to allow clipboard operations to complete before next selection
        // Wrapped as a Func because ThrottleDebounce 3.0.0's Action overloads throw RuntimeBinderException on execution
        _debouncedClipSelection = Debouncer.Debounce(() =>
        {
            ProcessClipSelection();
            return true;
        }, TimeSpan.FromMilliseconds(400));

        // Register this ViewModel as a recipient for clipboard and selection events
        // The messenger will automatically handle weak references and cleanup
        _messenger.Register<ClipAddedEvent>(this);
        _messenger.Register<ClipContentUpdatedEvent>(this);
        _messenger.Register<ClipCacheExpiredMessage>(this);
        _messenger.Register<CollectionNodeSelectedEvent>(this);
        _messenger.Register<RemoteDatabaseNodeSelectedEvent>(this);
        _messenger.Register<RemoteSearchResultsSelectedEvent>(this);
        _messenger.Register<QuickPasteNowEvent>(this);
        _messenger.Register<SelectNextClipEvent>(this);
        _messenger.Register<SelectPreviousClipEvent>(this);
        _messenger.Register<SelectClipRequestedEvent>(this);
        _messenger.Register<SearchResultsSelectedEvent>(this);
        _messenger.Register<ShowAllClipsInChildrenRequestedEvent>(this);
        _messenger.Register<ClipsDeletedEvent>(this);

        // The Peer of an open Remote Database view says when that collection changed
        _remoteDatabaseService.RemoteCollectionChanged += OnRemoteCollectionChanged;
    }

    /// <summary>
    /// Disposes the view model and unregisters from messenger.
    /// </summary>
    public void Dispose()
    {
        _messenger.UnregisterAll(this);
        _remoteDatabaseService.RemoteCollectionChanged -= OnRemoteCollectionChanged;
        _debouncedClipSelection?.Dispose();
        _remoteLoadCts?.Cancel();
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

            // Auto-select the new clip without copying it back: the clipboard already holds what the source offered
            SetSelectedClipWithoutClipboardOperation(message.Clip);
        });
    }

    /// <summary>
    /// Receives ClipCacheExpiredMessage when a temporarily decrypted clip's cache expires.
    /// Updates the clip's IsDecrypted flag and icon to show it's re-locked.
    /// </summary>
    public void Receive(ClipCacheExpiredMessage message)
    {
        _logger.LogInformation("ClipCacheExpiredMessage received for clip {ClipId}", message.ClipId);

        // Find the clip in the ObservableCollection
        var clip = Clips.FirstOrDefault(c => c.Id == message.ClipId);
        if (clip == null)
        {
            _logger.LogDebug("Clip {ClipId} not found in current view, ignoring cache expiration", message.ClipId);
            return;
        }

        // Update on UI thread
        Application.Current.Dispatcher.Invoke(() =>
        {
            // Clear the IsDecrypted flag
            clip.IsDecrypted = false;

            // Update icon: 🔓 → 🔒
            if (clip.IconGlyph.StartsWith("🔓"))
            {
                clip.IconGlyph = clip.IconGlyph.Replace("🔓", "🔒");
                _logger.LogInformation("Re-locked clip {ClipId} - changed icon from 🔓 to 🔒", message.ClipId);
            }

            // Send ClipUpdatedMessage to refresh the grid row
            _messenger.Send(new ClipUpdatedMessage(clip.Id, clip.Title));
        });
    }

    /// <summary>
    /// Receives ClipContentUpdatedEvent when an existing clip's content changed in place
    /// (e.g. Auto-Append mode merging a new capture into the growing clip). Updates the same
    /// clip object instance bound to the grid, then requests a row refresh.
    /// </summary>
    public void Receive(ClipContentUpdatedEvent message)
    {
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var clip = Clips.FirstOrDefault(p => p.Id == message.ClipId);
            if (clip == null)
                return;

            clip.TextContent = message.TextContent;
            clip.Title = message.Title;
            clip.Size = message.Size;

            _messenger.Send(new ClipUpdatedMessage(clip.Id, clip.Title));
        });
    }

    /// <summary>
    /// Receives ClipsDeletedEvent when clips have been deleted.
    /// Removes the deleted clips from the collection without reloading.
    /// </summary>
    public void Receive(ClipsDeletedEvent message)
    {
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            foreach (var item in message.DeletedClipIds)
            {
                var clipToRemove = Clips.FirstOrDefault(p => p.Id == item);
                if (clipToRemove == null)
                    continue;

                Clips.Remove(clipToRemove);

                // If the deleted clip was selected, clear selection
                if (SelectedClip?.Id == item)
                    SelectedClip = Clips.FirstOrDefault();
            }

            _logger.LogInformation("Removed {Count} clip(s) from UI collection", message.DeletedClipIds.Count);
        });
    }

    /// <summary>
    /// The most clips asked for in one page from a Peer.
    /// </summary>
    public const int RemotePageSize = 200;

    /// <summary>
    /// Receives RemoteDatabaseNodeSelectedEvent when a node in a Remote Database is selected: loads the clips of the
    /// selected collection or folder from the Peer, page by page, as a read-only list. Leaves the active collection
    /// alone, so the Remote Database never becomes the capture target.
    /// </summary>
    public async void Receive(RemoteDatabaseNodeSelectedEvent message)
    {
        _logger.LogInformation("RemoteDatabaseNodeSelectedEvent received: RemoteDatabase={RemoteDatabase}, CollectionId={CollectionId}",
            message.Address, message.CollectionId);

        try
        {
            if (message.CollectionId is { } collectionId)
                await LoadRemoteClipsAsync(message.Address, collectionId, message.FolderId);
            else
            {
                // The Remote Database node itself lists no clips
                CancelRemoteLoad();
                BeginRemoteView(message.Address, null, null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show Remote Database {RemoteDatabase}", message.Address);
        }
    }

    /// <summary>
    /// Receives RemoteSearchResultsSelectedEvent when a Remote Database's Search Results node is selected: shows the
    /// Remote Search results cached on this Device, read-only, and how many of the matches the Peer returned. Leaves the
    /// active collection alone.
    /// </summary>
    public void Receive(RemoteSearchResultsSelectedEvent message)
    {
        _logger.LogInformation("RemoteSearchResultsSelectedEvent received: RemoteDatabase={RemoteDatabase}, Count={Count}, TotalMatches={TotalMatches}",
            message.Address, message.Result.Clips.Count, message.Result.TotalMatches);

        CancelRemoteLoad();
        BeginRemoteView(message.Address, null, null);
        foreach (var item in message.Result.Clips)
            Clips.Add(item);

        SetSelectedClipWithoutClipboardOperation(Clips.FirstOrDefault());
        RemoteStatusMessage = message.Result.IsTruncated
            ? $"Showing {message.Result.Clips.Count:N0} of {message.Result.TotalMatches:N0} matches"
            : null;
    }

    /// <summary>
    /// Shows the clips of a collection or folder of a Remote Database, fetched from its Peer page by page: the first
    /// page appears as soon as it arrives and the rest are appended while the user works. Selecting another node
    /// cancels the load. A Peer that is offline or too slow ends the load with <see cref="RemoteStatusMessage" /> set,
    /// never a hang. Must be called on the UI thread.
    /// </summary>
    public Task LoadRemoteClipsAsync(RemoteDatabaseAddress address, Guid collectionId, Guid? folderId,
        CancellationToken cancellationToken = default) =>
        LoadRemoteClipsAsync(address, collectionId, folderId, null, cancellationToken);

    /// <summary>
    /// Re-fetches the open Remote Database view when its Peer reports that the collection or folder it shows changed
    /// (live change notifications). Raised on a background thread.
    /// </summary>
    private void OnRemoteCollectionChanged(object? sender, RemoteCollectionChangedEventArgs e)
    {
        UiThread.Run(() => _ = ReloadChangedRemoteViewAsync(e));
    }

    private async Task ReloadChangedRemoteViewAsync(RemoteCollectionChangedEventArgs change)
    {
        try
        {
            if (!IsRemoteDatabase || CurrentCollectionId is not { } collectionId || !RemoteDatabaseKey.TryParse(CurrentDatabaseKey, out var address) ||
                address != change.Address || change.CollectionId != (CurrentFolderId ?? collectionId))
                return;

            _logger.LogDebug("Remote collection {CollectionId} of {RemoteDatabase} changed; re-fetching", change.CollectionId, address);
            await LoadRemoteClipsAsync(address, collectionId, CurrentFolderId, SelectedClip?.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to re-fetch changed Remote Database collection {CollectionId}", change.CollectionId);
        }
    }

    private async Task LoadRemoteClipsAsync(RemoteDatabaseAddress address, Guid collectionId, Guid? folderId, Guid? reselectClipId,
        CancellationToken cancellationToken = default)
    {
        CancelRemoteLoad();
        BeginRemoteView(address, collectionId, folderId);
        using var load = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _remoteLoadCts = load;
        var token = load.Token;

        IsLoading = true;
        try
        {
            var offset = 0;
            while (true)
            {
                var page = await _remoteDatabaseService.GetClipPageAsync(address, collectionId, folderId, offset, RemotePageSize, token);
                token.ThrowIfCancellationRequested();

                foreach (var item in page.Clips)
                    Clips.Add(item);

                // A re-fetch keeps the clip that was selected, wherever it now is
                if (reselectClipId is { } clipId && page.Clips.FirstOrDefault(p => p.Id == clipId) is { } reselect)
                {
                    SetSelectedClipWithoutClipboardOperation(reselect);
                    reselectClipId = null;
                }
                else if (offset == 0 && reselectClipId == null)
                    SetSelectedClipWithoutClipboardOperation(Clips.FirstOrDefault());

                offset += page.Clips.Count;
                if (!page.HasMore)
                    break;

                RemoteStatusMessage = $"Loading clips… {offset:N0} of {page.TotalCount:N0}";
            }

            if (reselectClipId != null)
                SetSelectedClipWithoutClipboardOperation(Clips.FirstOrDefault());

            RemoteStatusMessage = null;
            _logger.LogInformation("Loaded {Count} clips from Remote Database {RemoteDatabase}", Clips.Count, address);
        }
        catch (OperationCanceledException)
        {
            // Another node was selected, or the view went away
        }
        catch (RemoteDatabaseUnavailableException ex)
        {
            if (!token.IsCancellationRequested)
                RemoteStatusMessage = ex.Message;

            _logger.LogInformation("Could not load the clips of Remote Database {RemoteDatabase}: {Reason}", address, ex.Message);
        }
        finally
        {
            if (ReferenceEquals(_remoteLoadCts, load))
            {
                IsLoading = false;
                _remoteLoadCts = null;
            }
        }
    }

    /// <summary>
    /// Switches the list to a read-only view of a Remote Database, never showing the previous clips as if they were the Peer's.
    /// </summary>
    private void BeginRemoteView(RemoteDatabaseAddress address, Guid? collectionId, Guid? folderId)
    {
        IsRemoteDatabase = true;
        AcceptsNewClips = false;
        RemoteStatusMessage = null;
        CurrentDatabaseKey = RemoteDatabaseKey.From(address);
        CurrentCollectionId = collectionId;
        CurrentFolderId = folderId;
        SelectedClip = null;
        SelectedClips.Clear();
        Clips.Clear();
    }

    /// <summary>
    /// Leaves a Remote Database view when a local node is selected.
    /// </summary>
    private void EndRemoteView()
    {
        CancelRemoteLoad();
        IsRemoteDatabase = false;
        RemoteStatusMessage = null;
    }

    private void CancelRemoteLoad()
    {
        var previous = _remoteLoadCts;
        if (previous == null)
            return;

        _remoteLoadCts = null;
        previous.Cancel();
        IsLoading = false;
    }

    /// <summary>
    /// Receives CollectionNodeSelectedEvent messages from the messenger.
    /// Loads clips for the selected collection/folder.
    /// </summary>
    public async void Receive(CollectionNodeSelectedEvent message)
    {
        _logger.LogInformation("CollectionNodeSelectedEvent received: DatabaseKey={DatabaseKey}, CollectionId={CollectionId}, FolderId={FolderId}, IsTrashcan={IsTrashcan}",
            message.DatabaseKey, message.CollectionId, message.FolderId, message.IsTrashcan);

        EndRemoteView();

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

        EndRemoteView();

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
        UiThread.Run(() =>
        {
            if (Clips.Count == 0)
            {
                _logger.LogDebug("Scroll {Direction} ignored: the clip list is empty", "Next");
                return;
            }

            var currentIndex = SelectedClip is not null
                ? Clips.IndexOf(SelectedClip)
                : -1;

            MoveTo(Clips[(currentIndex + 1) % Clips.Count], message.FromHotkey);
        });
    }

    /// <summary>
    /// Handles SelectPreviousClipEvent by moving selection to the previous clip in the list.
    /// </summary>
    public void Receive(SelectPreviousClipEvent message)
    {
        UiThread.Run(() =>
        {
            if (Clips.Count == 0)
            {
                _logger.LogDebug("Scroll {Direction} ignored: the clip list is empty", "Previous");
                return;
            }

            var currentIndex = SelectedClip is not null
                ? Clips.IndexOf(SelectedClip)
                : -1;

            var previousIndex = currentIndex <= 0
                ? Clips.Count - 1
                : currentIndex - 1;

            MoveTo(Clips[previousIndex], message.FromHotkey);
        });
    }

    /// <summary>
    /// Moves the selection to <paramref name="clip" /> for Scroll Next / Previous, announcing it when a hotkey asked, so
    /// the hotkey navigation popup can show it.
    /// </summary>
    private void MoveTo(Clip clip, bool fromHotkey)
    {
        // In a one-clip list the press stays where it is: nothing to announce
        if (clip == SelectedClip)
        {
            _logger.LogDebug("Scroll ignored: already on the only clip {ClipId}", clip.Id);
            return;
        }

        SelectedClip = clip;

        // Keep the grid's highlighted/selected row in sync with the navigated-to clip.
        // SelectedClip alone drives clipboard "flip" logic; SelectedClips is what the
        // DevExpress grid's row highlight is actually bound to (see GridControlSelectionSync).
        SelectedClips.Clear();
        SelectedClips.Add(clip);

        if (fromHotkey)
            _messenger.Send(new ClipReachedByHotkeyEvent(clip, CurrentDatabaseKey));
    }

    /// <summary>
    /// Receives ShowAllClipsInChildrenRequestedEvent when user requests to see all clips in a collection and its child
    /// folders.
    /// Loads clips recursively from the collection and all subfolders.
    /// </summary>
    public async void Receive(ShowAllClipsInChildrenRequestedEvent message)
    {
        _logger.LogInformation("ShowAllClipsInChildrenRequestedEvent received: CollectionId={CollectionId}, DatabaseKey={DatabaseKey}",
            message.CollectionId, message.DatabaseKey);

        EndRemoteView();

        // Store the current database key and collection ID
        CurrentDatabaseKey = message.DatabaseKey;
        CurrentCollectionId = message.CollectionId;

        // These views accept new clips
        AcceptsNewClips = true;

        // Load clips recursively
        await LoadClipsByCollectionRecursiveAsync(message.CollectionId, message.DatabaseKey);
    }

    /// <summary>
    /// Sets the SelectedClip property while temporarily suppressing clipboard operations.
    /// Use this during initial load/navigation to prevent unwanted clipboard side effects.
    /// </summary>
    private void SetSelectedClipWithoutClipboardOperation(Clip? clip)
    {
        _clipNotToCopyOnSelection = clip?.Id;
        SelectedClip = clip;
    }

    /// <summary>
    /// Selects a clip of the list without copying it to the clipboard or treating it as the user's pick, as Power Paste
    /// does to show the clip waiting to be pasted. Only the current clip moves; the selected rows stay selected, so a
    /// restart in the other direction still sees the whole sequence. Does nothing if the clip is not in the list.
    /// </summary>
    public void SelectClipWithoutCopying(Guid clipId)
    {
        var clip = Clips.FirstOrDefault(p => p.Id == clipId);
        if (clip != null && clip != SelectedClip)
            SetSelectedClipWithoutClipboardOperation(clip);
    }

    /// <summary>
    /// Selects the clip asked for without copying it (see <see cref="SelectClipWithoutCopying" />).
    /// </summary>
    public void Receive(SelectClipRequestedEvent message) => UiThread.Run(() => SelectClipWithoutCopying(message.ClipId));

    partial void OnSelectedClipChanged(Clip? value)
    {
        // Reset IsDecrypted flag on previous clip when navigating away
        // This ensures temporary decryption prompts again on next selection
        if (_previousSelectedClip != null && _previousSelectedClip != value)
            _previousSelectedClip.IsDecrypted = false;

        _previousSelectedClip = value;

        _logger.LogInformation("[ClipListViewModel] SelectedClip changed to: {ClipId}, Title: {Title}, DatabaseKey: {DatabaseKey}",
            value?.Id, value?.DisplayTitle, CurrentDatabaseKey);

        // Debounce clip selection to only process the final clip after navigation stops
        // This prevents UI flickering and wasted operations during rapid arrow key navigation
        _debouncedClipSelection.Invoke();
    }

    /// <summary>
    /// Processes the debounced clip selection.
    /// Called only after selection has stabilized (400ms with no further changes).
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
            var isUserSelection = clip.Id != _clipNotToCopyOnSelection;
            _messenger.Send(new ClipSelectedEvent(clip, CurrentDatabaseKey, isUserSelection));

            // Skip the clipboard for a clip selected without copying (just captured, or selected while loading)
            if (!isUserSelection)
                return;

            _clipNotToCopyOnSelection = null;

            // Power Paste holds the clipboard; picking a clip of its sequence moved the sequence there instead
            if (_powerPasteStatus.IsActive)
                return;

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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ClipListViewModel] Failed to set clipboard content for clip: {ClipId}", clip.Id);
        }
    }

    /// <summary>
    /// Loads a clip's full blob content (TextContent, ImageData, etc.) from the database into its
    /// transient properties, without touching the clipboard. Clips from the grid only carry
    /// metadata until hydrated - callers that inspect content (e.g. PowerPaste) must call this first.
    /// </summary>
    public async Task LoadBlobDataAsync(Clip clip, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(CurrentDatabaseKey))
            throw new InvalidOperationException("No database is currently selected");

        await _clipService.LoadBlobDataAsync(CurrentDatabaseKey, clip, cancellationToken);
    }

    /// <summary>
    /// Gets the currently selected clips ordered to match their position in the displayed grid
    /// (top to bottom), rather than whatever order the grid's selection API happens to report
    /// them in (e.g. click/selection order). Used by PowerPaste, where "Up"/"Down" direction only
    /// makes sense relative to the on-screen list order.
    /// </summary>
    public IReadOnlyList<Clip> GetSelectedClipsInDisplayOrder()
    {
        var displayIndexById = new Dictionary<Guid, int>();
        for (var i = 0; i < Clips.Count; i++)
            displayIndexById[Clips[i].Id] = i;

        return SelectedClips
            .OrderBy(p => displayIndexById.TryGetValue(p.Id, out var index)
                ? index
                : int.MaxValue)
            .ToList();
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
                // Auto-select the first (most recent) clip without triggering clipboard operation
                SetSelectedClipWithoutClipboardOperation(Clips.FirstOrDefault());
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

                if (collection is { IsVirtual: true } && !string.IsNullOrWhiteSpace(collection.Sql))
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
                // Auto-select the first (most recent) clip without triggering clipboard operation
                SetSelectedClipWithoutClipboardOperation(Clips.FirstOrDefault());
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
    /// Loads clips for a collection and all its child folders recursively.
    /// </summary>
    /// <param name="collectionId">The collection ID.</param>
    /// <param name="databaseKey">The database key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task LoadClipsByCollectionRecursiveAsync(Guid collectionId, string databaseKey, CancellationToken cancellationToken = default)
    {
        try
        {
            IsLoading = true;
            CurrentCollectionId = collectionId;
            CurrentFolderId = null;
            AcceptsNewClips = true; // This is a normal collection view

            _logger.LogInformation("Loading clips recursively for collection: {CollectionId}", collectionId);

            // Use the new recursive service method
            var clips = await _clipService.GetByCollectionRecursiveAsync(databaseKey, collectionId, false, cancellationToken);
            _logger.LogInformation("Retrieved {Count} clips recursively from collection {CollectionId}", clips.Count, collectionId);

            // Update collection on UI thread
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Clips.Clear();
                foreach (var clip in clips)
                    Clips.Add(clip);

                _logger.LogInformation("Updated UI collection: {Count} clips now in Clips collection", Clips.Count);
                // Auto-select the first (most recent) clip without triggering clipboard operation
                SetSelectedClipWithoutClipboardOperation(Clips.FirstOrDefault());
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading clips recursively for collection: {CollectionId}", collectionId);
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
                // Auto-select the first (most recent) clip without triggering clipboard operation
                SetSelectedClipWithoutClipboardOperation(Clips.FirstOrDefault());
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
                // Auto-select the first (most recent) clip without triggering clipboard operation
                SetSelectedClipWithoutClipboardOperation(Clips.FirstOrDefault());
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
                // Auto-select the first (most recent) clip without triggering clipboard operation
                SetSelectedClipWithoutClipboardOperation(Clips.FirstOrDefault());
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
        if (IsRemoteDatabase)
        {
            if (RemoteDatabaseKey.TryParse(CurrentDatabaseKey, out var address) && CurrentCollectionId.HasValue)
                await LoadRemoteClipsAsync(address, CurrentCollectionId.Value, CurrentFolderId);

            return;
        }

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
