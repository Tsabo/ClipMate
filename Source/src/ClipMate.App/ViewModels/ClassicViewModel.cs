using System.Collections.ObjectModel;
using ClipMate.Core.Models;
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
    [ObservableProperty]
    private bool _isDroppedDown;

    [ObservableProperty]
    private bool _isTacked;

    [ObservableProperty]
    private Clip? _selectedClip;

    [ObservableProperty]
    private ObservableCollection<Clip> _selectedClips = [];

    [ObservableProperty]
    private bool _shouldCloseWindow;

    public ClassicViewModel(MainMenuViewModel mainMenu,
        QuickPasteToolbarViewModel quickPasteToolbarViewModel,
        ClipListViewModel clipListViewModel)
    {
        MainMenu = mainMenu ?? throw new ArgumentNullException(nameof(mainMenu));
        QuickPasteToolbarViewModel = quickPasteToolbarViewModel ?? throw new ArgumentNullException(nameof(quickPasteToolbarViewModel));
        ClipListViewModel = clipListViewModel ?? throw new ArgumentNullException(nameof(clipListViewModel));
    }

    /// <summary>
    /// Shared main menu ViewModel.
    /// </summary>
    public MainMenuViewModel MainMenu { get; }

    /// <summary>
    /// ViewModel for the QuickPaste toolbar.
    /// </summary>
    public QuickPasteToolbarViewModel QuickPasteToolbarViewModel { get; }

    public ClipListViewModel ClipListViewModel { get; }

    /// <summary>
    /// Collection of available collections.
    /// </summary>
    public ObservableCollection<string> Collections { get; } = ["Inbox", "Safe", "Overflow", "Samples", "Trash Can"];

    // ==========================
    // Window State Commands
    // ==========================

    [RelayCommand]
    private void ToggleDropDown() => IsDroppedDown = !IsDroppedDown;

    [RelayCommand]
    private void ToggleTack() => IsTacked = !IsTacked;

    [RelayCommand]
    private void CloseWindow() => ShouldCloseWindow = true;
}
