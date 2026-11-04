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
    /// Starts a sequence of the clips, replacing any sequence already running. The clips are exploded when the
    /// PowerPasteExplode setting is on, and the direction is saved as the last one used.
    /// </summary>
    /// <param name="clips">The clips, in on-screen order.</param>
    /// <param name="direction">The direction to paste them in.</param>
    /// <param name="loadContent">Loads a clip's content when it comes up; leave it out when the clips are already loaded.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StartAsync(IReadOnlyList<Clip> clips, PowerPasteDirection direction, Func<Clip, Task>? loadContent = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// The Power Paste button: when on it stops, and when off it starts in the last direction used. Stopping without a
    /// paste flips that direction, so clicking again goes the other way.
    /// </summary>
    /// <param name="getClips">Gives the clips to paste in a direction when a sequence is started; none leaves Power Paste off.</param>
    /// <param name="loadContent">Loads a clip's content when it comes up.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ToggleAsync(Func<PowerPasteDirection, IReadOnlyList<Clip>> getClips, Func<Clip, Task>? loadContent = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// The Power Paste Up or Down command: stops when already going that way, otherwise starts going that way.
    /// </summary>
    /// <param name="direction">The command's direction.</param>
    /// <param name="getClips">Gives the clips to paste in a direction when a sequence is started; none leaves Power Paste as it is.</param>
    /// <param name="loadContent">Loads a clip's content when it comes up.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ToggleAsync(PowerPasteDirection direction, Func<PowerPasteDirection, IReadOnlyList<Clip>> getClips, Func<Clip, Task>? loadContent = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves the sequence to a clip in it, so that clip, from its first Explode piece, is the next one pasted. A clip
    /// outside the sequence sends it back to its start.
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
