using ClipMate.Core.Models;

namespace ClipMate.Core.Events;

/// <summary>
/// Event published when a new clip has been successfully saved to the database.
/// Allows ViewModels to update their UI in response to new clipboard captures.
/// </summary>
public class ClipAddedEvent
{
    public ClipAddedEvent(Clip clip, bool wasDuplicate, Guid? collectionId = null, Guid? folderId = null)
    {
        Clip = clip ?? throw new ArgumentNullException(nameof(clip));
        WasDuplicate = wasDuplicate;
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
    /// The collection ID the clip was assigned to.
    /// </summary>
    public Guid? CollectionId { get; }

    /// <summary>
    /// The folder ID the clip was assigned to.
    /// </summary>
    public Guid? FolderId { get; }
}
