using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ClipMate.Core.Models;
using ClipMate.Core.Services;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the clip list pane (middle pane).
/// Displays clips in list or grid view, handles selection, and loading.
/// </summary>
public partial class ClipListViewModel : ObservableObject
{
    private readonly IClipService _clipService;

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

    public ClipListViewModel(IClipService clipService)
    {
        _clipService = clipService ?? throw new ArgumentNullException(nameof(clipService));
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
    /// Refreshes the clip list by reloading from the service.
    /// </summary>
    public async Task RefreshAsync()
    {
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
