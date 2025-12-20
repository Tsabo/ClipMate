namespace ClipMate.Core.Models;

/// <summary>
/// Purge policy for collections.
/// </summary>
public enum PurgePolicy
{
    /// <summary>
    /// Never purge clips (safe collection).
    /// </summary>
    Never,

    /// <summary>
    /// Keep only the last N clips (based on RetentionLimit).
    /// </summary>
    KeepLast,

    /// <summary>
    /// Purge clips older than specified days.
    /// </summary>
    PurgeByAge,
}
