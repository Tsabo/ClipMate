namespace ClipMate.Core.Models.Configuration;

/// <summary>
/// Specifies when database cleanup (aging and purging) should occur.
/// </summary>
public enum CleanupMethod
{
    /// <summary>
    /// Never run automatic cleanup.
    /// </summary>
    Never = 0,

    /// <summary>
    /// Run cleanup manually only.
    /// </summary>
    Manual = 1,

    /// <summary>
    /// Run cleanup at application startup.
    /// </summary>
    AtStartup = 2,

    /// <summary>
    /// Run cleanup at application shutdown.
    /// </summary>
    AtShutdown = 3,

    /// <summary>
    /// Run cleanup after every hour of idle time.
    /// </summary>
    AfterHourIdle = 4,
}
