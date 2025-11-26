using ClipMate.App.ViewModels;
using ClipMate.App.Views;

namespace ClipMate.App.Services;

/// <summary>
/// Singleton service for managing ClipViewerWindow instance.
/// </summary>
public class ClipViewerWindowManager : IClipViewerWindowManager
{
    private readonly Func<ClipViewerViewModel> _viewModelFactory;
    private ClipViewerWindow? _window;

    public ClipViewerWindowManager(Func<ClipViewerViewModel> viewModelFactory)
    {
        _viewModelFactory = viewModelFactory ?? throw new ArgumentNullException(nameof(viewModelFactory));
    }

    public bool IsOpen => _window?.IsVisible == true;

    public void ShowClipViewer(Guid clipId)
    {
        if (_window == null)
        {
            // Create window on first use
            _window = new ClipViewerWindow(_viewModelFactory());
        }

        _window.LoadAndShow(clipId);
    }

    public void CloseClipViewer() => _window?.Hide();
}
