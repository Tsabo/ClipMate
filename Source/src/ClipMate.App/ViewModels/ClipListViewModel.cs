using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Application = System.Windows.Application;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the clip list pane (middle pane).
/// Displays clips in list or grid view, handles selection, and loading.
/// Implements IRecipient to receive ClipAddedEvent messages via MVVM Toolkit Messenger.
/// </summary>
public partial class ClipListViewModel : ObservableObject, IRecipient<ClipAddedEvent>
{
    private readonly IClipService _clipService;
    private readonly IMessenger _messenger;

    [ObservableProperty]
    private ObservableCollection<Clip> _clips = [];

    [ObservableProperty]
    private Clip? _selectedClip;

    [ObservableProperty]
    private bool _isListView = true;

    [ObservableProperty]
    private bool _isGridView = false;

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private Guid? _currentCollectionId;

    [ObservableProperty]
    private Guid? _currentFolderId;

    public ClipListViewModel(IClipService clipService, IMessenger messenger)
    {
        _clipService = clipService ?? throw new ArgumentNullException(nameof(clipService));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));

        // Register this ViewModel as a recipient for ClipAddedEvent
        // The messenger will automatically handle weak references and cleanup
        _messenger.Register(this);
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
    /// Determines if a clip should be displayed in the current view based on active filters.
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
    /// Loads clips from the clip service.
    /// </summary>
    /// <param name="count">Maximum number of clips to load (default 1000).</param>
    public async Task LoadClipsAsync(int count = 1000)
    {
        try
        {
            IsLoading = true;
            var clips = await _clipService.GetRecentAsync(count);
            
            // Clear and repopulate the existing collection to maintain binding
            Clips.Clear();
            foreach (var clip in clips)
            {
                Clips.Add(clip);
            }
        }
        catch
        {
            // Handle exceptions gracefully - don't crash the UI
            Clips.Clear();
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

            var clips = await _clipService.GetByCollectionAsync(collectionId, cancellationToken);
            
            // Clear and repopulate
            Clips.Clear();
            foreach (var clip in clips)
            {
                Clips.Add(clip);
            }
        }
        catch
        {
            Clips.Clear();
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

            var clips = await _clipService.GetByFolderAsync(folderId, cancellationToken);
            
            // Clear and repopulate
            Clips.Clear();
            foreach (var clip in clips)
            {
                Clips.Add(clip);
            }
        }
        catch
        {
            Clips.Clear();
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
