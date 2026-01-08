namespace ClipMate.App.Models.TreeNodes;

/// <summary>
/// Type of tree node in the collection hierarchy.
/// </summary>
[Flags]
public enum TreeNodeType
{
    None = 0,

    /// <summary>
    /// Database root node (e.g., "My Clips").
    /// </summary>
    Database = 1 << 1,

    /// <summary>
    /// Regular collection node (e.g., "Inbox", "Safe", "Important Stuff").
    /// </summary>
    Collection = 1 << 2,

    /// <summary>
    /// Folder within a collection.
    /// </summary>
    Folder = 1 << 3,

    /// <summary>
    /// Virtual collections container node.
    /// </summary>
    VirtualCollectionsContainer = 1 << 4,

    /// <summary>
    /// Virtual collection (pre-defined search, e.g., "Today", "Bitmaps").
    /// </summary>
    VirtualCollection = 1 << 5,

    /// <summary>
    /// Special collection (e.g., "Trash Can", "Search Results").
    /// </summary>
    SpecialCollection = 1 << 6,

    /// <summary>
    /// Application profile node for clipboard format filtering.
    /// </summary>
    ApplicationProfile = 1 << 7,

    /// <summary>
    /// Clipboard format node under an application profile.
    /// </summary>
    ApplicationProfileFormat = 1 << 8,
}
