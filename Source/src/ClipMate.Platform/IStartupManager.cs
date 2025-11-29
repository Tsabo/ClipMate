namespace ClipMate.Platform;

/// <summary>
/// Service for managing Windows startup configuration (Run registry key).
/// </summary>
public interface IStartupManager
{
    /// <summary>
    /// Checks if ClipMate is configured to start with Windows.
    /// </summary>
    /// <returns>
    /// A tuple containing:
    /// - success: True if the check succeeded, false if there was an error accessing the registry.
    /// - isEnabled: True if startup is enabled, false otherwise.
    /// - errorMessage: Error message if success is false, otherwise null.
    /// </returns>
    Task<(bool success, bool isEnabled, string? errorMessage)> IsEnabledAsync();

    /// <summary>
    /// Enables ClipMate to start automatically with Windows.
    /// </summary>
    /// <returns>
    /// A tuple containing:
    /// - success: True if the operation succeeded, false if there was an error.
    /// - errorMessage: Error message if success is false, otherwise null.
    /// </returns>
    Task<(bool success, string? errorMessage)> EnableAsync();

    /// <summary>
    /// Disables ClipMate from starting automatically with Windows.
    /// </summary>
    /// <returns>
    /// A tuple containing:
    /// - success: True if the operation succeeded, false if there was an error.
    /// - errorMessage: Error message if success is false, otherwise null.
    /// </returns>
    Task<(bool success, string? errorMessage)> DisableAsync();
}
