using System.Collections.ObjectModel;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for ClipMate Classic window with collapsible clip list.
/// Features stay-on-top, auto-collapse, dropdown/tack state management.
/// Delegates menu commands to MainMenuViewModel.
/// </summary>
public partial class ClassicViewModel : ObservableObject
{
    private readonly IClipService _clipService;

    [ObservableProperty]
    private bool _isDroppedDown;

    [ObservableProperty]
    private bool _isTacked;

    [ObservableProperty]
    private string _powerPasteStatus = string.Empty;

    [ObservableProperty]
    private string _powerPasteTarget = "Target: None";

    [ObservableProperty]
    private Clip? _selectedClip;

    [ObservableProperty]
    private string _selectedClipTitle = "No clip selected";

    [ObservableProperty]
    private bool _shouldCloseWindow;

    public ClassicViewModel(IClipService clipService, MainMenuViewModel mainMenu)
    {
        _clipService = clipService ?? throw new ArgumentNullException(nameof(clipService));
        MainMenu = mainMenu ?? throw new ArgumentNullException(nameof(mainMenu));
    }

    /// <summary>
    /// Shared main menu ViewModel.
    /// </summary>
    public MainMenuViewModel MainMenu { get; }

    /// <summary>
    /// Collection of clips in the current collection.
    /// </summary>
    public ObservableCollection<Clip> Clips { get; } = [];

    /// <summary>
    /// Collection of available collections.
    /// </summary>
    public ObservableCollection<string> Collections { get; } = ["Inbox", "Safe", "Overflow", "Samples", "Trash Can"];

    /// <summary>
    /// Loads the most recent clips from the current collection.
    /// </summary>
    public async Task LoadRecentClipsAsync(int count = 50, CancellationToken cancellationToken = default)
    {
        var clips = await _clipService.GetRecentAsync(count, cancellationToken);

        Clips.Clear();
        foreach (var item in clips)
            Clips.Add(item);

        if (Clips.Count > 0)
        {
            SelectedClip = Clips[0];
            SelectedClipTitle = SelectedClip.Title ?? SelectedClip.TextContent ?? "Untitled";
        }
    }

    // ==========================
    // Window State Commands
    // ==========================

    [RelayCommand]
    private void ToggleDropDown() => IsDroppedDown = !IsDroppedDown;

    [RelayCommand]
    private void ToggleTack() => IsTacked = !IsTacked;

    [RelayCommand]
    private void CloseWindow() => ShouldCloseWindow = true;

    // ==========================
    // Status Bar Commands (Window-specific)
    // ==========================

    [RelayCommand]
    private void Home() { }

    [RelayCommand]
    private void Target() { }
}
