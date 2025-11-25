namespace ClipMate.Core.Models;

/// <summary>
/// Purging rules for automatic clip deletion in collections.
/// Maps to ClipMate 7.5 behavior.
/// </summary>
public enum PurgingRule
{
    /// <summary>
    /// Keep only the last N items in the collection.
    /// Oldest clips are automatically deleted when limit is exceeded.
    /// </summary>
    ByNumberOfItems,

    /// <summary>
    /// Delete clips older than specified number of days.
    /// </summary>
    ByAge,

    /// <summary>
    /// Never automatically delete clips ("Safe" collection).
    /// RetentionLimit = 0 in database.
    /// </summary>
    Never
}
