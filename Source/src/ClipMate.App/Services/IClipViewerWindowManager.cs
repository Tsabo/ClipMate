namespace ClipMate.App.Services;

/// <summary>
/// Manages the single instance of ClipViewerWindow.
/// </summary>
public interface IClipViewerWindowManager
{
    /// <summary>
    /// Gets whether the clip viewer window is currently open.
    /// </summary>
    bool IsOpen { get; }

    /// <summary>
    /// Shows the clip viewer window with the specified clip.
    /// </summary>
    void ShowClipViewer(Guid clipId);

    /// <summary>
    /// Closes the clip viewer window if open.
    /// </summary>
    void CloseClipViewer();
}
