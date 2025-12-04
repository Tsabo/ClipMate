namespace ClipMate.Core.Models;

/// <summary>
/// Represents different types of sound events in the application.
/// </summary>
public enum SoundEvent
{
    /// <summary>
    /// Sound played when the clipboard is updated with new content.
    /// </summary>
    ClipboardUpdate,

    /// <summary>
    /// Sound played when clips are appended together.
    /// </summary>
    Append,

    /// <summary>
    /// Sound played when clips are erased.
    /// </summary>
    Erase,

    /// <summary>
    /// Sound played when clips are filtered.
    /// </summary>
    Filter,

    /// <summary>
    /// Sound played when clipboard content is ignored.
    /// </summary>
    Ignore,

    /// <summary>
    /// Sound played when PowerPaste operation completes.
    /// </summary>
    PowerPasteComplete,
}
