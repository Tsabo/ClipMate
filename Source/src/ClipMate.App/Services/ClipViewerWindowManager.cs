using System;
using ClipMate.App.Views;
using ClipMate.Core.ViewModels;

namespace ClipMate.App.Services;

/// <summary>
/// Manages the single instance of ClipViewerWindow.
/// </summary>
public interface IClipViewerWindowManager
{
    /// <summary>
    /// Shows the clip viewer window with the specified clip.
    /// </summary>
    void ShowClipViewer(Guid clipId);

    /// <summary>
    /// Closes the clip viewer window if open.
    /// </summary>
    void CloseClipViewer();

    /// <summary>
    /// Gets whether the clip viewer window is currently open.
    /// </summary>
    bool IsOpen { get; }
}

/// <summary>
/// Singleton service for managing ClipViewerWindow instance.
/// </summary>
public class ClipViewerWindowManager : IClipViewerWindowManager
{
    private readonly Func<ClipViewerViewModel> _viewModelFactory;
    private ClipViewerWindow? _window;

    public bool IsOpen => _window?.IsVisible == true;

    public ClipViewerWindowManager(Func<ClipViewerViewModel> viewModelFactory)
    {
        _viewModelFactory = viewModelFactory ?? throw new ArgumentNullException(nameof(viewModelFactory));
    }

    public void ShowClipViewer(Guid clipId)
    {
        if (_window == null)
        {
            // Create window on first use
            _window = new ClipViewerWindow(_viewModelFactory());
        }

        _window.LoadAndShow(clipId);
    }

    public void CloseClipViewer()
    {
        _window?.Hide();
    }
}
