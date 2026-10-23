using ClipMate.Core.Models;

namespace ClipMate.Core.Events;

/// <summary>
/// Messenger event sent when a clip is selected in the ClipList.
/// Allows PreviewPane and other ViewModels to react without direct coupling.
/// </summary>
public class ClipSelectedEvent
{
    /// <summary>
    /// Creates a new clip selection event.
    /// </summary>
    /// <param name="selectedClip">The selected clip, or null if cleared.</param>
    /// <param name="databaseKey">The database key where the clip is stored.</param>
    /// <param name="isUserSelection">False when ClipMate selected the clip itself (loading a list, a new capture, following Power Paste).</param>
    public ClipSelectedEvent(Clip? selectedClip, string? databaseKey = null, bool isUserSelection = true)
    {
        SelectedClip = selectedClip;
        DatabaseKey = databaseKey;
        IsUserSelection = isUserSelection;
    }

    /// <summary>
    /// The selected clip, or null if selection was cleared.
    /// </summary>
    public Clip? SelectedClip { get; }

    /// <summary>
    /// The database key where the clip is stored, or null if no clip is selected.
    /// </summary>
    public string? DatabaseKey { get; }

    /// <summary>
    /// True when the user picked the clip; false when ClipMate selected it itself (loading a list, a new capture,
    /// following Power Paste), which does not copy it to the clipboard.
    /// </summary>
    public bool IsUserSelection { get; }
}
