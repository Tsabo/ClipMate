using ClipMate.Core.ValueObjects;

namespace ClipMate.Core.Services;

/// <summary>
/// Service for checking for ClipMate application updates from GitHub releases.
/// </summary>
public interface IUpdateCheckService
{
    /// <summary>
    /// Checks GitHub releases for a newer version of the application.
    /// </summary>
    /// <param name="currentVersion">Current application version (e.g., "1.0.0")</param>
    /// <param name="includePrerelease">Whether to include pre-release versions</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ApplicationVersion if a newer version is available, null otherwise</returns>
    Task<ApplicationVersion?> CheckForUpdatesAsync(string currentVersion,
        bool includePrerelease = false,
        CancellationToken cancellationToken = default);
}
