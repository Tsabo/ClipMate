namespace ClipMate.Core.Models;

/// <summary>
/// Type of collection in ClipMate.
/// Maps to LmType field in Collection model.
/// </summary>
public enum CollectionType
{
    /// <summary>
    /// Normal collection (LmType = 0).
    /// Standard collection that stores clips.
    /// </summary>
    Normal = 0,

    /// <summary>
    /// Folder within a collection (LmType = 2).
    /// Can contain subfolders and organize clips hierarchically.
    /// </summary>
    Folder = 2,

    /// <summary>
    /// Trash can collection (special normal collection).
    /// Deleted clips go here before permanent deletion.
    /// </summary>
    Trashcan = 3,

    /// <summary>
    /// Virtual collection (LmType = 1, ListType = 1 or 3).
    /// Dynamic collection based on SQL query.
    /// </summary>
    Virtual = 1
}
