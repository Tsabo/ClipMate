using ClipMate.Core.Models;

namespace ClipMate.Core.Services;

/// <summary>
/// Service for monitoring and capturing clipboard changes.
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// Event raised when a new clip is captured from the clipboard.
    /// </summary>
    event EventHandler<ClipCapturedEventArgs>? ClipCaptured;

    /// <summary>
    /// Starts monitoring the clipboard for changes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StartMonitoringAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops monitoring the clipboard.
    /// </summary>
    Task StopMonitoringAsync();

    /// <summary>
    /// Gets the current clipboard content as a Clip entity (without saving).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The clipboard content as a Clip; null if clipboard is empty.</returns>
    Task<Clip?> GetCurrentClipboardContentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the clipboard content from a Clip entity.
    /// </summary>
    /// <param name="clip">The clip to set to the clipboard.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetClipboardContentAsync(Clip clip, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether clipboard monitoring is currently active.
    /// </summary>
    bool IsMonitoring { get; }
}

/// <summary>
/// Event args for the ClipCaptured event.
/// </summary>
public class ClipCapturedEventArgs : EventArgs
{
    /// <summary>
    /// The captured clip.
    /// </summary>
    public required Clip Clip { get; init; }

    /// <summary>
    /// Whether to cancel saving this clip (set by event handlers).
    /// </summary>
    public bool Cancel { get; set; }
}
