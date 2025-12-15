namespace ClipMate.Core.Events;

/// <summary>
/// Messenger event sent when a collection tree node is selected.
/// Allows ViewModels to respond to navigation changes without direct coupling.
/// </summary>
public class CollectionNodeSelectedEvent
{
    /// <summary>
    /// Creates a new collection node selection event.
    /// </summary>
    /// <param name="collectionId">The selected collection ID.</param>
    /// <param name="folderId">The selected folder ID, or null for collection-level selection.</param>
    /// <param name="databaseKey">The database key (configuration key) where this collection resides.</param>
    /// <param name="isTrashcan">True if this is the Trashcan virtual collection.</param>
    public CollectionNodeSelectedEvent(Guid collectionId, Guid? folderId, string databaseKey, bool isTrashcan = false)
    {
        CollectionId = collectionId;
        FolderId = folderId;
        DatabaseKey = databaseKey ?? throw new ArgumentNullException(nameof(databaseKey));
        IsTrashcan = isTrashcan;
    }

    /// <summary>
    /// The ID of the selected collection.
    /// </summary>
    public Guid CollectionId { get; }

    /// <summary>
    /// The ID of the selected folder, or null if a collection is selected.
    /// </summary>
    public Guid? FolderId { get; }

    /// <summary>
    /// The database configuration key where this collection resides.
    /// </summary>
    public string DatabaseKey { get; }

    /// <summary>
    /// True if this is the Trashcan virtual collection (shows all deleted clips).
    /// </summary>
    public bool IsTrashcan { get; }
}
