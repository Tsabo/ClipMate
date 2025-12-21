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
    public ClipSelectedEvent(Clip? selectedClip, string? databaseKey = null)
    {
        SelectedClip = selectedClip;
        DatabaseKey = databaseKey;
    }

    /// <summary>
    /// The selected clip, or null if selection was cleared.
    /// </summary>
    public Clip? SelectedClip { get; }

    /// <summary>
    /// The database key where the clip is stored, or null if no clip is selected.
    /// </summary>
    public string? DatabaseKey { get; }
}
