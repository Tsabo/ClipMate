using ClipMate.Core.Models;

namespace ClipMate.Core.Events;

/// <summary>
/// Messenger event sent by the Current Clip writer, on the UI thread, when it has put a clip on the system clipboard.
/// Allows the status bar to show confirmation without direct coupling; <see cref="CurrentClipChangedEvent" /> also
/// covers captures.
/// </summary>
public class ClipboardCopiedEvent
{
    /// <summary>
    /// Creates a new clipboard copied event.
    /// </summary>
    /// <param name="clip">The clip that was copied to the clipboard.</param>
    public ClipboardCopiedEvent(Clip clip)
    {
        Clip = clip;
    }

    /// <summary>
    /// The clip that was copied to the clipboard.
    /// </summary>
    public Clip Clip { get; }
}
