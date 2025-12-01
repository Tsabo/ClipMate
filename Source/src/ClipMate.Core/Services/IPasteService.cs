using ClipMate.Core.Models;

namespace ClipMate.Core.Services;

/// <summary>
/// Service for pasting clipboard content to the active window.
/// </summary>
public interface IPasteService
{
    /// <summary>
    /// Pastes the specified clip content to the currently active window.
    /// </summary>
    /// <param name="clip">The clip to paste.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the paste was successful; otherwise, false.</returns>
    Task<bool> PasteToActiveWindowAsync(Clip clip, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the title of the currently active window.
    /// </summary>
    /// <returns>The window title, or empty string if unable to retrieve.</returns>
    string GetActiveWindowTitle();

    /// <summary>
    /// Gets the process name of the currently active window.
    /// </summary>
    /// <returns>The process name, or empty string if unable to retrieve.</returns>
    string GetActiveWindowProcessName();
}
