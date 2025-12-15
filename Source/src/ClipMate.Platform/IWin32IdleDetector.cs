namespace ClipMate.Platform;

/// <summary>
/// Service for detecting Windows user idle time.
/// Uses GetLastInputInfo and GetTickCount64 Win32 APIs.
/// </summary>
public interface IWin32IdleDetector
{
    /// <summary>
    /// Gets the time in milliseconds since the last user input event.
    /// Returns 0 if unable to determine idle time.
    /// </summary>
    /// <returns>Milliseconds since last input.</returns>
    uint GetIdleTimeMilliseconds();

    /// <summary>
    /// Checks if the system has been idle for at least the specified duration.
    /// </summary>
    /// <param name="idleThreshold">Minimum idle duration to check.</param>
    /// <returns>True if idle time exceeds threshold; otherwise false.</returns>
    bool IsIdle(TimeSpan idleThreshold);
}
