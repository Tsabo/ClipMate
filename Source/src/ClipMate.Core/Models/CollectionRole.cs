namespace ClipMate.Core.Models;

/// <summary>
/// Special role or purpose of a collection in retention/clip management.
/// Determines special handling for Overflow and Trashcan collections.
/// Most collections have no special role (None).
/// </summary>
public enum CollectionRole
{
    /// <summary>
    /// No special role (default).
    /// Normal user-created collection with standard retention behavior.
    /// </summary>
    None = 0,

    /// <summary>
    /// Overflow collection.
    /// Receives clips moved from collections exceeding retention limits.
    /// When Overflow itself exceeds limits, clips move to Trashcan.
    /// </summary>
    Overflow = 1,

    /// <summary>
    /// Trash can collection.
    /// Deleted clips go here before permanent deletion.
    /// Acts as final destination in retention cascade: Collection → Overflow → Trashcan.
    /// </summary>
    Trashcan = 2,
}
