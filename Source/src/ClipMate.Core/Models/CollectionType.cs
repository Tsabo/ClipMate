namespace ClipMate.Core.Models;

/// <summary>
/// Type of collection in ClipMate.
/// High-level categorization for business logic.
/// </summary>
public enum CollectionType
{
    /// <summary>
    /// Normal collection.
    /// Standard collection that stores clips.
    /// </summary>
    Normal = 0,

    /// <summary>
    /// Virtual collection.
    /// Dynamic collection based on SQL query.
    /// </summary>
    Virtual = 1,

    /// <summary>
    /// Folder within a collection.
    /// Can contain subfolders and organize clips hierarchically.
    /// </summary>
    Folder = 2,

    /// <summary>
    /// Trash can collection (special normal collection).
    /// Deleted clips go here before permanent deletion.
    /// </summary>
    Trashcan = 3
}
