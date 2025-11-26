namespace ClipMate.Core.Models;

/// <summary>
/// List mode type (LMTYPE in ClipMate 7.5).
/// Determines the fundamental type of collection/folder.
/// </summary>
public enum CollectionLmType
{
    /// <summary>
    /// Normal collection (LmType = 0).
    /// Standard collection that stores clips.
    /// </summary>
    Normal = 0,

    /// <summary>
    /// Virtual collection (LmType = 1).
    /// Dynamic collection based on SQL query.
    /// </summary>
    Virtual = 1,

    /// <summary>
    /// Folder within a collection (LmType = 2).
    /// Can contain subfolders and organize clips hierarchically.
    /// </summary>
    Folder = 2
}
