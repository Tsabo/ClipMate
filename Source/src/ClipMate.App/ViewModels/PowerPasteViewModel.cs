using System.Collections.ObjectModel;
using System.ComponentModel;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the PowerPaste quick access window.
/// </summary>
public partial class PowerPasteViewModel : ObservableObject
{
    private readonly IClipService _clipService;
    private readonly IPasteService _pasteService;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _selectedIndex;

    [ObservableProperty]
    private bool _shouldCloseWindow;

    public PowerPasteViewModel(IClipService clipService, IPasteService pasteService)
    {
        _clipService = clipService ?? throw new ArgumentNullException(nameof(clipService));
        _pasteService = pasteService ?? throw new ArgumentNullException(nameof(pasteService));

        PropertyChanged += OnPropertyChanged;
    }

    /// <summary>
    /// Collection of all loaded clips.
    /// </summary>
    public ObservableCollection<Clip> Clips { get; } = [];

    /// <summary>
    /// Collection of filtered clips based on search text.
    /// </summary>
    public ObservableCollection<Clip> FilteredClips { get; } = [];

    /// <summary>
    /// Loads the most recent clips.
    /// </summary>
    /// <param name="count">Number of clips to load. Defaults to 20.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task LoadRecentClipsAsync(int count = 20, CancellationToken cancellationToken = default)
    {
        var clips = await _clipService.GetRecentAsync(count, cancellationToken);

        Clips.Clear();
        foreach (var item in clips)
            Clips.Add(item);

        UpdateFilteredClips();
        SelectedIndex = 0;
    }

    /// <summary>
    /// Selects a clip and pastes it to the active window.
    /// </summary>
    [RelayCommand]
    private async Task SelectClipAsync(Clip? clip)
    {
        if (clip == null)
            return;

        var success = await _pasteService.PasteToActiveWindowAsync(clip);
        if (success)
            ShouldCloseWindow = true;
    }

    /// <summary>
    /// Cancels the PowerPaste operation and closes the window.
    /// </summary>
    [RelayCommand]
    private void Cancel() => ShouldCloseWindow = true;

    /// <summary>
    /// Navigates to the previous item in the list.
    /// </summary>
    [RelayCommand]
    private void NavigateUp()
    {
        if (FilteredClips.Count == 0)
            return;

        SelectedIndex--;
        if (SelectedIndex < 0)
            SelectedIndex = FilteredClips.Count - 1; // Wrap to last
    }

    /// <summary>
    /// Navigates to the next item in the list.
    /// </summary>
    [RelayCommand]
    private void NavigateDown()
    {
        if (FilteredClips.Count == 0)
            return;

        SelectedIndex++;
        if (SelectedIndex >= FilteredClips.Count)
            SelectedIndex = 0; // Wrap to first
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SearchText))
            UpdateFilteredClips();
    }

    private void UpdateFilteredClips()
    {
        FilteredClips.Clear();

        var filteredItems = string.IsNullOrWhiteSpace(SearchText)
            ? Clips
            : Clips.Where(p => (p.TextContent ?? string.Empty).Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        foreach (var item in filteredItems)
            FilteredClips.Add(item);

        // Reset selection if current index is out of range
        if (SelectedIndex >= FilteredClips.Count)
            SelectedIndex = FilteredClips.Count > 0
                ? 0
                : -1;
    }
}
