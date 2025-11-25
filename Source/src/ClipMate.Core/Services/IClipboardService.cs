using System.Threading.Channels;
using ClipMate.Core.Models;

namespace ClipMate.Core.Services;

/// <summary>
/// Service interface for clipboard monitoring and manipulation.
/// Uses a channel-based pattern for captured clips to enable proper async handling and backpressure.
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// Gets a channel reader for consuming captured clipboard clips.
    /// The consumer reads from this channel to process clipboard changes asynchronously.
    /// </summary>
    ChannelReader<Clip> ClipsChannel { get; }

    /// <summary>
    /// Gets a value indicating whether clipboard monitoring is active.
    /// </summary>
    bool IsMonitoring { get; }

    /// <summary>
    /// Starts monitoring the system clipboard for changes.
    /// Captured clips are published to the <see cref="ClipsChannel"/>.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to stop monitoring.</param>
    /// <returns>A task that completes when monitoring has started.</returns>
    Task StartMonitoringAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops monitoring the system clipboard.
    /// Completes the clips channel, preventing further writes.
    /// </summary>
    /// <returns>A task that completes when monitoring has stopped.</returns>
    Task StopMonitoringAsync();

    /// <summary>
    /// Gets the current clipboard content without starting monitoring.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current clipboard content, or null if clipboard is empty or an error occurs.</returns>
    Task<Clip?> GetCurrentClipboardContentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the system clipboard content from a clip.
    /// </summary>
    /// <param name="clip">The clip to place on the clipboard.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the clipboard has been updated.</returns>
    Task SetClipboardContentAsync(Clip clip, CancellationToken cancellationToken = default);
}
