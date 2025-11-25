using ClipMate.Core.Models;

namespace ClipMate.Core.Events;

/// <summary>
/// Messenger event sent when a clip is selected in the ClipList.
/// Allows PreviewPane and other ViewModels to react without direct coupling.
/// </summary>
public class ClipSelectedEvent
{
    /// <summary>
    /// The selected clip, or null if selection was cleared.
    /// </summary>
    public Clip? SelectedClip { get; }

    /// <summary>
    /// Creates a new clip selection event.
    /// </summary>
    /// <param name="selectedClip">The selected clip, or null if cleared.</param>
    public ClipSelectedEvent(Clip? selectedClip)
    {
        SelectedClip = selectedClip;
    }
}
