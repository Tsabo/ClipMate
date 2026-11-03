using ClipMate.Core.Models;

namespace ClipMate.Core.Services.Abstractions;

/// <summary>
/// Power Paste: pastes a sequence of clips, or the Explode pieces of them, one per paste. It owns the sequence, offers
/// the waiting clip on the clipboard by delayed rendering, tells a paste from other reads, and holds capture while it
/// runs.
/// </summary>
public interface IPowerPaste
{
    /// <summary>
    /// Gets the current state of Power Paste.
    /// </summary>
    PowerPasteState State { get; }

    /// <summary>
    /// Gets the direction the sequence is pasted in.
    /// </summary>
    PowerPasteDirection Direction { get; }

    /// <summary>
    /// Gets the number of steps in the sequence.
    /// </summary>
    int TotalCount { get; }

    /// <summary>
    /// Raised when Power Paste starts or stops.
    /// </summary>
    event EventHandler<PowerPasteStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Raised when another clip of the sequence is waiting to be pasted.
    /// </summary>
    event EventHandler<PowerPastePositionChangedEventArgs>? PositionChanged;

    /// <summary>
    /// Starts a sequence of the clips, with their content loaded, replacing any sequence already running. The clips are
    /// exploded when the PowerPasteExplode setting is on, and the direction is saved as the last one used.
    /// </summary>
    /// <param name="clips">The clips, in on-screen order.</param>
    /// <param name="direction">The direction to paste them in.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StartAsync(IReadOnlyList<Clip> clips, PowerPasteDirection direction, CancellationToken cancellationToken = default);

    /// <summary>
    /// The Power Paste button: off starts going down, down starts again going up, and up stops.
    /// </summary>
    /// <param name="loadSelection">Loads the clips to paste when a sequence is started; none leaves Power Paste as it is.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ToggleAsync(Func<Task<IReadOnlyList<Clip>>> loadSelection, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves the sequence to a clip in it, so that clip is the next one pasted: its first Explode piece going down,
    /// its last going up.
    /// </summary>
    /// <param name="clipId">The clip's ID.</param>
    /// <returns>False if Power Paste is not active or the clip is not in the sequence.</returns>
    bool MoveTo(Guid clipId);

    /// <summary>
    /// Stops the sequence, leaving the clip that was waiting on the clipboard as a normal copy.
    /// </summary>
    void Stop();

    /// <summary>
    /// Stops the sequence.
    /// </summary>
    /// <param name="keepWaitingClip">
    /// True to leave the clip that was waiting on the clipboard as a normal copy (the user stopped Power Paste); false
    /// when something else is about to put its own content there (another copy, picking a clip outside the sequence).
    /// </param>
    void Stop(bool keepWaitingClip);
}
