using System.Collections.ObjectModel;
using ClipMate.App.Models;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using ClipMate.Core.Services.Abstractions;
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
    private readonly IDatabases _databases;
    private readonly RateLimitedFunc<bool> _debouncedClipSelection;
    private readonly IFolderService _folderService;
    private readonly ILogger<ClipListViewModel> _logger;
    private readonly IMessenger _messenger;
    private readonly PowerPasteStatus _powerPasteStatus;
    private readonly IQuickPasteService _quickPasteService;
    private readonly IRemoteDatabaseService _remoteDatabaseService;
    private ClipListScope? _currentScope;
    private CancellationTokenSource? _loadCts;

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
    /// Whether the list shows clips of a Remote Database, for Copy to local (its clips are copied, never moved). Never
    /// checked to refuse a change; that is <see cref="IsReadOnly" />.
    /// </summary>
    [ObservableProperty]
    private bool _isRemoteDatabase;

    /// <summary>
    /// Whether the list shows a Database nothing may be written to (every Remote Database): nothing in this view may
    /// modify, move, rename or delete its clips, and it never receives captures.
    /// </summary>
    [ObservableProperty]
    private bool _isReadOnly;

    /// <summary>
    /// Progress while a Database's clips load page by page, or why they could not be loaded (file missing, Peer offline,
    /// timeout); <c>null</c> when there is nothing to report.
    /// </summary>
    [ObservableProperty]
    private string? _remoteStatusMessage;

    /// <summary>
    /// Whether the list shows a Search Results node (local or Remote), whose clips each carry their
    /// <see cref="Clip.CollectionDisplay" /> for the Collection column.
    /// </summary>
    [ObservableProperty]
    private bool _showsSearchResults;

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
        IDatabases databases,
        IRemoteDatabaseService remoteDatabaseService,
        IMessenger messenger,
        ILogger<ClipListViewModel> logger)
    {
        _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
        _folderService = folderService ?? throw new ArgumentNullException(nameof(folderService));
        _clipService = clipService ?? throw new ArgumentNullException(nameof(clipService));
        _quickPasteService = quickPasteService ?? throw new ArgumentNullException(nameof(quickPasteService));
        _powerPasteStatus = powerPasteStatus ?? throw new ArgumentNullException(nameof(powerPasteStatus));
        _databases = databases ?? throw new ArgumentNullException(nameof(databases));
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
        _loadCts?.Cancel();
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
    /// The most clips asked for in one page.
    /// </summary>
    public const int PageSize = 200;

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
            var databaseKey = RemoteDatabaseKey.From(message.Address);
            if (message.CollectionId is { } collectionId)
            {
                await LoadAsync(databaseKey, message.FolderId is { } folderId
                    ? new ClipListScope.Folder(collectionId, folderId)
                    : new ClipListScope.Collection(collectionId));
            }
            else
            {
                // The Remote Database node itself lists no clips
                CancelLoad();
                BeginView(databaseKey, null, null);
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
    public async void Receive(RemoteSearchResultsSelectedEvent message)
    {
        _logger.LogInformation("RemoteSearchResultsSelectedEvent received: RemoteDatabase={RemoteDatabase}, Count={Count}, TotalMatches={TotalMatches}",
            message.Address, message.Result.Clips.Count, message.Result.TotalMatches);

        using var load = new CancellationTokenSource();
        try
        {
            CancelLoad();
            var database = BeginView(RemoteDatabaseKey.From(message.Address), null, null);
            ShowsSearchResults = true;
            _loadCts = load;
            foreach (var item in message.Result.Clips)
                Clips.Add(item);

            SetSelectedClipWithoutClipboardOperation(Clips.FirstOrDefault());
            if (message.Result.IsTruncated)
                RemoteStatusMessage = $"Showing {message.Result.Clips.Count:N0} of {message.Result.TotalMatches:N0} matches";

            // The matches show at once; the Collection column fills in when the Peer's tree arrives
            await NameCollectionsAsync(database, message.Result.Clips, load.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show the Remote Search results of {RemoteDatabase}", message.Address);
        }
        finally
        {
            if (ReferenceEquals(_loadCts, load))
                _loadCts = null;
        }
    }

    /// <summary>
    /// Shows the clips of <paramref name="scope" /> in the Database that <paramref name="databaseKey" /> names, local or
    /// remote, fetched page by page: the first page appears as soon as it arrives and the rest are appended while the
    /// user works. Selecting another node cancels the load. A Database that cannot be read (its file is missing, its Peer
    /// is offline or too slow) ends the load with <see cref="RemoteStatusMessage" /> set, never a hang. Must be called on
    /// the UI thread.
    /// </summary>
    public Task LoadAsync(string databaseKey, ClipListScope scope, CancellationToken cancellationToken = default) =>
        LoadAsync(databaseKey, scope, null, cancellationToken);

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
            if (_currentScope == null || CurrentCollectionId is not { } collectionId || CurrentDatabaseKey == null ||
                CurrentDatabaseKey != RemoteDatabaseKey.From(change.Address) || change.CollectionId != (CurrentFolderId ?? collectionId))
                return;

            _logger.LogDebug("Remote collection {CollectionId} of {RemoteDatabase} changed; re-fetching", change.CollectionId, change.Address);
            await LoadAsync(CurrentDatabaseKey, _currentScope, SelectedClip?.Id, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to re-fetch changed Remote Database collection {CollectionId}", change.CollectionId);
        }
    }

    private async Task LoadAsync(string databaseKey, ClipListScope scope, Guid? reselectClipId, CancellationToken cancellationToken)
    {
        CancelLoad();
        var (collectionId, folderId) = scope switch
        {
            ClipListScope.Collection collection => (collection.CollectionId, (Guid?)null),
            ClipListScope.Folder folder => (folder.CollectionId, folder.FolderId),
            var _ => ((Guid?)null, (Guid?)null),
        };

        var database = BeginView(databaseKey, collectionId, folderId);
        _currentScope = scope;
        using var load = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loadCts = load;
        var token = load.Token;

        IsLoading = true;
        try
        {
            if (database == null)
                return;

            AcceptsNewClips = await AcceptsNewClipsAsync(database, scope, token);
            var offset = 0;
            while (true)
            {
                var page = await database.GetClipPageAsync(scope, offset, PageSize, token);
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
            _logger.LogInformation("Loaded {Count} clips from Database {DatabaseKey}", Clips.Count, databaseKey);
        }
        catch (OperationCanceledException)
        {
            // Another node was selected, or the view went away
        }
        catch (DatabaseUnavailableException ex)
        {
            if (!token.IsCancellationRequested)
                RemoteStatusMessage = ex.Message;

            _logger.LogInformation("Could not load the clips of Database {DatabaseKey}: {Reason}", databaseKey, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading the clips of Database {DatabaseKey}", databaseKey);
        }
        finally
        {
            if (ReferenceEquals(_loadCts, load))
            {
                IsLoading = false;
                _loadCts = null;
            }
        }
    }

    /// <summary>
    /// Whether clips captured while <paramref name="scope" /> is shown belong in it: not in a read-only Database, the
    /// Trash, or a Smart or SQL-based collection, which show what their query finds.
    /// </summary>
    private static async Task<bool> AcceptsNewClipsAsync(IDatabase database, ClipListScope scope, CancellationToken cancellationToken)
    {
        if (database.IsReadOnly || scope is ClipListScope.Trash)
            return false;

        if (scope is not ClipListScope.Collection { IncludeFolders: false } collection)
            return true;

        var tree = await database.GetTreeAsync(cancellationToken);
        return tree.FirstOrDefault(p => p.Id == collection.CollectionId)?.Kind is not (CollectionEntryKind.Smart or CollectionEntryKind.SqlBased);
    }

    /// <summary>
    /// Switches the list to a view of a Database, never showing the previous clips as if they were this one's. A
    /// read-only (Remote) Database's view never accepts new clips.
    /// </summary>
    /// <returns>The Database, or <c>null</c> if it cannot be found (its reason is in <see cref="RemoteStatusMessage" />).</returns>
    private IDatabase? BeginView(string databaseKey, Guid? collectionId, Guid? folderId)
    {
        RemoteStatusMessage = null;
        CurrentDatabaseKey = databaseKey;
        CurrentCollectionId = collectionId;
        CurrentFolderId = folderId;
        _currentScope = null;
        ShowsSearchResults = false;
        SelectedClip = null;
        SelectedClips.Clear();
        Clips.Clear();

        try
        {
            var database = _databases.Get(databaseKey);
            IsReadOnly = database.IsReadOnly;
            IsRemoteDatabase = database.Remote != null;
            AcceptsNewClips = !database.IsReadOnly;
            return database;
        }
        catch (DatabaseUnavailableException ex)
        {
            IsReadOnly = false;
            IsRemoteDatabase = false;
            AcceptsNewClips = false;
            RemoteStatusMessage = ex.Message;
            return null;
        }
    }

    private void CancelLoad()
    {
        var previous = _loadCts;
        if (previous == null)
            return;

        _loadCts = null;
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

        try
        {
            // Set the active collection and folder for new clipboard captures
            // Skip this for virtual collections like Trashcan (they don't receive new clips)
            if (!message.IsTrashcan)
            {
                await _collectionService.SetActiveAsync(message.CollectionId, message.DatabaseKey);
                await _folderService.SetActiveAsync(message.FolderId);

                _logger.LogInformation("Active collection and folder updated for new clipboard captures");
            }

            ClipListScope scope = message switch
            {
                { IsTrashcan: true } => new ClipListScope.Trash(),
                { FolderId: { } folderId } => new ClipListScope.Folder(message.CollectionId, folderId),
                var _ => new ClipListScope.Collection(message.CollectionId),
            };

            await LoadAsync(message.DatabaseKey, scope);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show collection {CollectionId} of Database {DatabaseKey}", message.CollectionId, message.DatabaseKey);
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

        await LoadAsync(message.DatabaseKey, new ClipListScope.Collection(message.CollectionId, IncludeFolders: true));
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
        CancelLoad();
        _currentScope = null;
        ShowsSearchResults = false;
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
    /// Loads specific clips by their IDs (used for search results). Must be called on the UI thread.
    /// </summary>
    /// <param name="clipIds">The list of clip IDs to load.</param>
    /// <param name="databaseKey">The database key to load clips from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task LoadClipsByIdsAsync(IReadOnlyList<Guid> clipIds, string databaseKey, CancellationToken cancellationToken = default)
    {
        CancelLoad();
        var database = BeginView(databaseKey, null, null);
        ShowsSearchResults = true;

        // Search results don't accept new clips
        AcceptsNewClips = false;

        try
        {
            IsLoading = true;
            _logger.LogInformation("Loading {Count} clips by IDs for database: {DatabaseKey}", clipIds.Count, databaseKey);

            foreach (var item in clipIds)
            {
                var clip = await _clipService.GetByIdAsync(databaseKey, item, cancellationToken);
                if (clip != null)
                    Clips.Add(clip);
            }

            _logger.LogInformation("Retrieved {Count} clips from database '{DatabaseKey}'", Clips.Count, databaseKey);

            // Auto-select the first (most recent) clip without triggering clipboard operation
            SetSelectedClipWithoutClipboardOperation(Clips.FirstOrDefault());
            await NameCollectionsAsync(database, Clips.ToList(), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading clips by IDs for database: {DatabaseKey}", databaseKey);
            Clips.Clear();
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Fills in the Collection column of search results already shown, from <paramref name="database" />'s tree, read
    /// once. A tree that cannot be read (the Peer went offline since the search, a damaged file) leaves the column
    /// empty and the results as they are; a tree that arrives after another view was shown is dropped.
    /// </summary>
    private async Task NameCollectionsAsync(IDatabase? database, IReadOnlyList<Clip> clips, CancellationToken cancellationToken)
    {
        if (database == null || clips.Count == 0)
            return;

        CollectionNames names;
        try
        {
            names = new CollectionNames(await database.GetTreeAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("Collection names of Database {DatabaseKey} dropped: another view was shown before its tree arrived", database.Key);
            return;
        }
        catch (DatabaseUnavailableException ex)
        {
            _logger.LogInformation("Could not read the tree of Database {DatabaseKey} to name the search results' collections: {Reason}", database.Key, ex.Message);
            return;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read the tree of Database {DatabaseKey} to name the search results' collections", database.Key);
            return;
        }

        foreach (var item in clips)
            item.CollectionDisplay = names.Describe(item);
    }

    /// <summary>
    /// The collection and folder names of one Database's tree, looked up by the IDs a clip carries. IDs repeat across
    /// Databases, so one is only ever used for the clips of its own Database.
    /// </summary>
    private sealed class CollectionNames(IReadOnlyList<CollectionEntry> tree)
    {
        private readonly Dictionary<Guid, CollectionEntry> _entries = tree.ToDictionary(p => p.Id);

        /// <summary>
        /// Names the clip's collection, then each folder down to its own ("Inbox › Projects"); empty if the tree has
        /// no entry for it.
        /// </summary>
        public string Describe(Clip clip)
        {
            if ((clip.FolderId ?? clip.CollectionId) is not { } id)
                return string.Empty;

            var names = new List<string>();
            var seen = new HashSet<Guid>();
            for (Guid? current = id; current is { } entryId && seen.Add(entryId) && _entries.TryGetValue(entryId, out var entry); current = entry.ParentId)
                names.Add(entry.Title);

            names.Reverse();
            return TreePath.Join(names);
        }
    }

    /// <summary>
    /// Refreshes the clip list by loading what it shows again.
    /// </summary>
    public async Task RefreshAsync()
    {
        if (CurrentDatabaseKey != null && _currentScope != null)
            await LoadAsync(CurrentDatabaseKey, _currentScope);
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
