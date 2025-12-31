using System.ComponentModel;
using ClipMate.App.Services;
using ClipMate.App.ViewModels;

namespace ClipMate.App.Views;

/// <summary>
/// Floating window for viewing individual clipboard entries.
/// Launched via F2 hotkey.
/// </summary>
public partial class ClipViewerWindow : INotifyPropertyChanged
{
    private readonly ClipViewerViewModel _viewModel;
    private readonly IClipViewerWindowManager _windowManager;

    public ClipViewerWindow(ClipViewerViewModel viewModel, IClipViewerWindowManager windowManager)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        DataContext = _viewModel;

        InitializeComponent();

        // Handle closing
        Closing += OnClosing;
    }

    /// <summary>
    /// Gets or sets whether the current clip is pinned.
    /// Delegates to the window manager for state management.
    /// </summary>
    public bool IsPinned
    {
        get => _windowManager.IsPinned;
        set
        {
            if (_windowManager.IsPinned != value)
            {
                _windowManager.IsPinned = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPinned)));
            }
        }
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Loads a clip by ID and shows/activates the window.
    /// Use this for explicit user actions (F2 hotkey).
    /// </summary>
    /// <param name="clipId">The clip ID to load.</param>
    /// <param name="databaseKey">The database key where the clip is stored.</param>
    public async void LoadAndShow(Guid clipId, string? databaseKey = null)
    {
        // Show window FIRST so WebView2 can initialize (it needs to be visible)
        Show();
        Activate();

        // Small delay to allow WebView2 to process NavigationCompleted
        // This is needed because WebView2 won't fire NavigationCompleted until rendered
        await Task.Delay(50);

        await _viewModel.LoadClipCommand.ExecuteAsync((clipId, databaseKey));
    }

    /// <summary>
    /// Updates the displayed clip without stealing focus.
    /// Use this for auto-follow when selection changes.
    /// </summary>
    /// <param name="clipId">The clip ID to load.</param>
    /// <param name="databaseKey">The database key where the clip is stored.</param>
    public async void UpdateClip(Guid clipId, string? databaseKey = null)
    {
        // If window is not visible, show it first so WebView2 can initialize
        if (!IsVisible)
        {
            Show();
            await Task.Delay(50);
        }

        await _viewModel.LoadClipCommand.ExecuteAsync((clipId, databaseKey));
        // Don't activate - just update content silently
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        // Just hide the window instead of closing it (for reuse)
        e.Cancel = true;
        Hide();
    }
}
