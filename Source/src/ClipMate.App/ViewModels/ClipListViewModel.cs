using System.Collections.ObjectModel;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Application = System.Windows.Application;

namespace ClipMate.App.ViewModels;

/// <summary>
///     ViewModel for the clip list pane (middle pane).
///     Displays clips in list or grid view, handles selection, and loading.
///     Implements IRecipient to receive ClipAddedEvent and CollectionNodeSelectedEvent messages via MVVM Toolkit
///     Messenger.
/// </summary>
public partial class ClipListViewModel : ObservableObject, IRecipient<ClipAddedEvent>, IRecipient<CollectionNodeSelectedEvent>
{
    private readonly IClipService _clipService;
    private readonly ICollectionService _collectionService;
    private readonly IFolderService _folderService;
    private readonly ILogger<ClipListViewModel> _logger;
    private readonly IMessenger _messenger;

    [ObservableProperty]
    private ObservableCollection<Clip> _clips = [];

    [ObservableProperty]
    private Guid? _currentCollectionId;

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

    public ClipListViewModel(IClipService clipService,
        IMessenger messenger,
        IFolderService folderService,
        ICollectionService collectionService,
        ILogger<ClipListViewModel> logger)
    {
        _clipService = clipService ?? throw new ArgumentNullException(nameof(clipService));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _folderService = folderService ?? throw new ArgumentNullException(nameof(folderService));
        _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Register this ViewModel as a recipient for clipboard and selection events
        // The messenger will automatically handle weak references and cleanup
        _messenger.Register<ClipAddedEvent>(this);
        _messenger.Register<CollectionNodeSelectedEvent>(this);
    }

    /// <summary>
    ///     Receives ClipAddedEvent messages from the messenger.
    ///     This method is called automatically by the messenger when a ClipAddedEvent is sent.
    /// </summary>
    public void Receive(ClipAddedEvent message)
    {
        // Ensure we're on the UI thread
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            // Check if this clip should be displayed in the current view
            var shouldDisplay = ShouldDisplayClip(message.Clip, message.CollectionId, message.FolderId);

            if (!shouldDisplay)
            {
                return;
            }

            // Check if clip already exists in the collection (duplicate handling)
            var existingClip = Clips.FirstOrDefault(c => c.Id == message.Clip.Id);

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
                {
                    Clips.RemoveAt(Clips.Count - 1);
                }
            }

            // Optional: Auto-select the new clip
            // SelectedClip = message.Clip;
        });
    }

    /// <summary>
    ///     Receives CollectionNodeSelectedEvent messages from the messenger.
    ///     Loads clips for the selected collection/folder.
    /// </summary>
    public async void Receive(CollectionNodeSelectedEvent message)
    {
        _logger.LogInformation("CollectionNodeSelectedEvent received: CollectionId={CollectionId}, FolderId={FolderId}",
            message.CollectionId, message.FolderId);

        // Set the active collection and folder for new clipboard captures
        await _collectionService.SetActiveAsync(message.CollectionId);
        await _folderService.SetActiveAsync(message.FolderId);

        _logger.LogInformation("Active collection and folder updated for new clipboard captures");

        // Load clips for the selected node
        if (message.FolderId.HasValue)
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

    partial void OnSelectedClipChanged(Clip? value)
    {
        // Send messenger event when selection changes
        _messenger.Send(new ClipSelectedEvent(value));
    }

    /// <summary>
    ///     Determines if a clip should be displayed in the current view based on active filters.
    /// </summary>
    private bool ShouldDisplayClip(Clip clip, Guid? clipCollectionId, Guid? clipFolderId)
    {
        // If viewing a specific folder, only show clips in that folder
        if (CurrentFolderId.HasValue && CurrentCollectionId.HasValue)
        {
            return clipFolderId == CurrentFolderId && clipCollectionId == CurrentCollectionId;
        }

        // If viewing a specific collection, only show clips in that collection
        if (CurrentCollectionId.HasValue)
        {
            return clipCollectionId == CurrentCollectionId;
        }

        // Default view - show all clips (or implement your default logic)
        return true;
    }

    /// <summary>
    ///     Loads clips from the clip service.
    /// </summary>
    /// <param name="count">Maximum number of clips to load (default 1000).</param>
    public async Task LoadClipsAsync(int count = 1000)
    {
        try
        {
            IsLoading = true;
            _logger.LogInformation("Loading recent {Count} clips (no collection filter)", count);
            var clips = await _clipService.GetRecentAsync(count);
            _logger.LogInformation("Retrieved {Count} recent clips. First clip CollectionId: {CollectionId}", clips.Count(), clips.FirstOrDefault()?.CollectionId);

            // Update collection on UI thread
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Clips.Clear();
                foreach (var clip in clips)
                {
                    Clips.Add(clip);
                }

                _logger.LogInformation("Updated UI collection: {Count} clips now in Clips collection", Clips.Count);
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
    ///     Loads clips for a specific collection.
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
            var clips = await _clipService.GetByCollectionAsync(collectionId, cancellationToken);
            _logger.LogInformation("Retrieved {Count} clips from database", clips.Count());

            // Update collection on UI thread
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Clips.Clear();
                var clipList = clips.ToList();
                foreach (var clip in clipList)
                {
                    Clips.Add(clip);
                }

                _logger.LogInformation("Updated UI collection: {Count} clips now in Clips collection", Clips.Count);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading clips for collection {CollectionId}", collectionId);
            await Application.Current.Dispatcher.InvokeAsync(() => Clips.Clear());
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    ///     Loads clips for a specific folder.
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

            var clips = await _clipService.GetByFolderAsync(folderId, cancellationToken);

            // Update collection on UI thread
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Clips.Clear();
                foreach (var clip in clips)
                {
                    Clips.Add(clip);
                }
            });
        }
        catch
        {
            await Application.Current.Dispatcher.InvokeAsync(() => Clips.Clear());
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    ///     Refreshes the clip list by reloading from the service.
    /// </summary>
    public async Task RefreshAsync()
    {
        if (CurrentFolderId.HasValue && CurrentCollectionId.HasValue)
        {
            await LoadClipsByFolderAsync(CurrentCollectionId.Value, CurrentFolderId.Value);
        }
        else if (CurrentCollectionId.HasValue)
        {
            await LoadClipsByCollectionAsync(CurrentCollectionId.Value);
        }
        else
        {
            await LoadClipsAsync();
        }
    }

    /// <summary>
    ///     Switches the view to list mode.
    /// </summary>
    public void SwitchToListView()
    {
        IsListView = true;
        IsGridView = false;
    }

    /// <summary>
    ///     Switches the view to grid mode.
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
