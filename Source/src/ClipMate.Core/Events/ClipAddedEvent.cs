using ClipMate.Core.Models;

namespace ClipMate.Core.Events;

/// <summary>
/// Event published when a new clip has been successfully saved to the database.
/// Allows ViewModels to update their UI in response to new clipboard captures.
/// </summary>
public class ClipAddedEvent
{
    public ClipAddedEvent(Clip clip, bool wasDuplicate, string databaseKey, Guid? collectionId = null, Guid? folderId = null)
    {
        Clip = clip ?? throw new ArgumentNullException(nameof(clip));
        WasDuplicate = wasDuplicate;
        DatabaseKey = databaseKey ?? throw new ArgumentNullException(nameof(databaseKey));
        CollectionId = collectionId;
        FolderId = folderId;
    }

    /// <summary>
    /// The clip that was added to the database.
    /// </summary>
    public Clip Clip { get; }

    /// <summary>
    /// Whether this was a new clip or a duplicate of an existing clip.
    /// </summary>
    public bool WasDuplicate { get; }

    /// <summary>
    /// The database key where the clip was saved.
    /// </summary>
    public string DatabaseKey { get; }

    /// <summary>
    /// The collection ID the clip was assigned to.
    /// </summary>
    public Guid? CollectionId { get; }

    /// <summary>
    /// The folder ID the clip was assigned to.
    /// </summary>
    public Guid? FolderId { get; }

    /// <summary>
    /// Whether the clip was captured from the clipboard, and so is the Current Clip; <c>false</c> for a clip ClipMate
    /// made without putting it on the clipboard, such as by Append Clips.
    /// </summary>
    public bool Captured { get; init; }
}
