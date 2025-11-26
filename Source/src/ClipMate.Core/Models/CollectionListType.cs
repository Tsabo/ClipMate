namespace ClipMate.Core.Models;

/// <summary>
/// List type (LISTTYPE in ClipMate 7.5).
/// Determines how the collection displays and filters content.
/// </summary>
public enum CollectionListType
{
    /// <summary>
    /// Normal list (ListType = 0).
    /// Standard list that shows all clips in the collection.
    /// </summary>
    Normal = 0,

    /// <summary>
    /// Smart/Virtual collection (ListType = 1).
    /// Dynamic list based on predefined criteria.
    /// </summary>
    Smart = 1,

    /// <summary>
    /// SQL-based collection (ListType = 3).
    /// Dynamic list based on custom SQL query.
    /// </summary>
    SqlBased = 3
}
