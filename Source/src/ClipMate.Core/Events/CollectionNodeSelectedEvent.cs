namespace ClipMate.Core.Events;

/// <summary>
/// Messenger event sent when a collection tree node is selected.
/// Allows ViewModels to respond to navigation changes without direct coupling.
/// </summary>
public class CollectionNodeSelectedEvent
{
    /// <summary>
    /// The ID of the selected collection.
    /// </summary>
    public Guid CollectionId { get; }

    /// <summary>
    /// The ID of the selected folder, or null if a collection is selected.
    /// </summary>
    public Guid? FolderId { get; }

    /// <summary>
    /// Creates a new collection node selection event.
    /// </summary>
    /// <param name="collectionId">The selected collection ID.</param>
    /// <param name="folderId">The selected folder ID, or null for collection-level selection.</param>
    public CollectionNodeSelectedEvent(Guid collectionId, Guid? folderId)
    {
        CollectionId = collectionId;
        FolderId = folderId;
    }
}
