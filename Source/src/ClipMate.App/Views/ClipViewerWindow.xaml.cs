using System.ComponentModel;
using ClipMate.Core.ViewModels;
using DevExpress.Xpf.Core;

namespace ClipMate.App.Views;

/// <summary>
///     Floating window for viewing individual clipboard entries.
///     Launched via F2 hotkey.
/// </summary>
public partial class ClipViewerWindow : ThemedWindow
{
    private readonly ClipViewerViewModel _viewModel;

    public ClipViewerWindow(ClipViewerViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;

        InitializeComponent();

        // Handle closing
        Closing += OnClosing;
    }

    /// <summary>
    ///     Loads a clip by ID and shows the window.
    /// </summary>
    public async void LoadAndShow(Guid clipId)
    {
        await _viewModel.LoadClipCommand.ExecuteAsync(clipId);
        Show();
        Activate();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        // Just hide the window instead of closing it (for reuse)
        e.Cancel = true;
        Hide();
    }
}
