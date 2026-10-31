using System.Threading.Channels;
using ClipMate.Core.Models;

namespace ClipMate.Core.Services.Abstractions;

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
    /// Changes dropped because they came within the settle time of the previous capture, since ClipMate started. At
    /// 100, as in ClipMate 7, monitored changes are no longer captured until ClipMate restarts.
    /// </summary>
    int SettleGapDrops { get; }

    /// <summary>
    /// Starts monitoring the system clipboard for changes.
    /// Captured clips are published to the <see cref="ClipsChannel" />.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to stop monitoring.</param>
    /// <returns>A task that completes when monitoring has started.</returns>
    Task StartMonitoringAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops monitoring the system clipboard. The clips channel stays open, so monitoring can start again; it
    /// completes when the service is disposed.
    /// </summary>
    /// <returns>A task that completes when monitoring has stopped.</returns>
    Task StopMonitoringAsync();

    /// <summary>
    /// Reads the clipboard now, for the Capture hotkey or at startup, without the delay or settle gap of a monitored
    /// change and whether or not monitoring is on. See <see cref="ManualCaptureKind" /> for each one's rules.
    /// </summary>
    /// <param name="kind">Which capture this is.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The clip, or null when there is nothing to capture or an error occurs.</returns>
    Task<Clip?> GetCurrentClipboardContentAsync(ManualCaptureKind kind = ManualCaptureKind.CaptureHotkey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads every Format on the clipboard for Capture Special, ignoring the ignore flag; null when the clipboard can't
    /// be read or capture is suspended.
    /// </summary>
    Task<ClipboardReading?> ReadClipboardAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Capture Special: the clip of the chosen Formats of <paramref name="reading" />, whatever the Application Profile
    /// says. Choosing a text, image or file-list Format brings its companions (Unicode/ANSI text and locale, every
    /// image encoding, the drop effect); null when nothing chosen can be kept.
    /// </summary>
    Task<Clip?> CaptureSpecialAsync(ClipboardReading reading, IReadOnlyCollection<string> formatNames, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the system clipboard content from a clip.
    /// </summary>
    /// <param name="clip">The clip to place on the clipboard.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the clipboard has been updated.</returns>
    Task SetClipboardContentAsync(Clip clip, CancellationToken cancellationToken = default);

    /// <summary>
    /// The Formats that <see cref="SetClipboardContentAsync" /> would put on the clipboard for a clip, as raw data,
    /// without touching the clipboard. Used by Power Paste to offer the clip by delayed rendering. Call on the UI thread.
    /// </summary>
    /// <param name="clip">The clip, with its content loaded.</param>
    /// <returns>One entry per clipboard format, empty if the clip has nothing to put on the clipboard.</returns>
    IReadOnlyList<ClipboardFormatData> GetClipboardFormats(Clip clip);

    /// <summary>
    /// Suspends reacting to clipboard change notifications without stopping monitoring or
    /// completing the clips channel. Used by features (e.g. PowerPaste) that repeatedly modify
    /// the clipboard themselves and would otherwise have those changes misread as new captures.
    /// </summary>
    void SuspendCapture();

    /// <summary>
    /// Resumes reacting to clipboard change notifications after a prior <see cref="SuspendCapture" />.
    /// </summary>
    void ResumeCapture();
}
