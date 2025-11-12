namespace ClipMate.Core.Models;

/// <summary>
/// Defines retention policies for automatic clip cleanup.
/// </summary>
public enum RetentionPolicy
{
    /// <summary>
    /// Never auto-delete clips.
    /// </summary>
    KeepAll = 0,

    /// <summary>
    /// Delete oldest clips when MaxClipCount is exceeded.
    /// </summary>
    LimitByCount = 1,

    /// <summary>
    /// Delete oldest clips when MaxTotalSize (bytes) is exceeded.
    /// </summary>
    LimitBySize = 2,

    /// <summary>
    /// Delete clips older than MaxClipAge (days).
    /// </summary>
    LimitByAge = 3,

    /// <summary>
    /// Apply both count and age limits.
    /// </summary>
    LimitByCountAndAge = 4
}
