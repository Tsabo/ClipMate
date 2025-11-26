namespace ClipMate.App.ViewModels;

/// <summary>
/// Type of tree node in the collection hierarchy.
/// </summary>
public enum TreeNodeType
{
    /// <summary>
    /// Database root node (e.g., "My Clips").
    /// </summary>
    Database,

    /// <summary>
    /// Regular collection node (e.g., "Inbox", "Safe", "Important Stuff").
    /// </summary>
    Collection,

    /// <summary>
    /// Folder within a collection.
    /// </summary>
    Folder,

    /// <summary>
    /// Virtual collections container node.
    /// </summary>
    VirtualCollectionsContainer,

    /// <summary>
    /// Virtual collection (pre-defined search, e.g., "Today", "Bitmaps").
    /// </summary>
    VirtualCollection,

    /// <summary>
    /// Special collection (e.g., "Trash Can", "Search Results").
    /// </summary>
    SpecialCollection,
}
