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
    /// Gets or sets whether the current clip is pinned.
    /// When pinned, clip selection changes are ignored until unpinned.
    /// </summary>
    bool IsPinned { get; set; }

    /// <summary>
    /// Shows the clip viewer window with the specified clip.
    /// Activates the window (steals focus).
    /// </summary>
    /// <param name="clipId">The ID of the clip to display.</param>
    /// <param name="databaseKey">The database key where the clip is stored.</param>
    void ShowClipViewer(Guid clipId, string? databaseKey = null);

    /// <summary>
    /// Updates the displayed clip without stealing focus.
    /// Use this for auto-follow when selection changes.
    /// </summary>
    /// <param name="clipId">The ID of the clip to display.</param>
    /// <param name="databaseKey">The database key where the clip is stored.</param>
    void UpdateClipViewer(Guid clipId, string? databaseKey = null);

    /// <summary>
    /// Toggles the floating viewer visibility.
    /// If closed, opens with the currently selected clip.
    /// If open, updates to the currently selected clip (clears pin state).
    /// </summary>
    void ToggleVisibility();

    /// <summary>
    /// Closes the clip viewer window if open.
    /// </summary>
    void CloseClipViewer();
}
