using ClipMate.Core.Models;

namespace ClipMate.Core.Services;

/// <summary>
/// Service for managing PowerPaste sequential automation.
/// PowerPaste automatically pastes clips one-by-one in sequence as the user performs paste operations.
/// </summary>
public interface IPowerPasteService
{
    /// <summary>
    /// Gets the current state of PowerPaste.
    /// </summary>
    PowerPasteState State { get; }

    /// <summary>
    /// Gets the current direction of PowerPaste traversal.
    /// </summary>
    PowerPasteDirection Direction { get; }

    /// <summary>
    /// Gets the current position in the PowerPaste sequence.
    /// </summary>
    int CurrentPosition { get; }

    /// <summary>
    /// Gets the total count of items in the PowerPaste sequence.
    /// </summary>
    int TotalCount { get; }

    /// <summary>
    /// Event raised when PowerPaste state changes.
    /// </summary>
    event EventHandler<PowerPasteStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Event raised when PowerPaste position advances.
    /// </summary>
    event EventHandler<PowerPastePositionChangedEventArgs>? PositionChanged;

    /// <summary>
    /// Starts a PowerPaste sequence with the specified clips.
    /// </summary>
    /// <param name="clips">The clips to include in the sequence.</param>
    /// <param name="direction">The direction to traverse (Up or Down).</param>
    /// <param name="explodeMode">If true, split clips into fragments using delimiters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StartAsync(IReadOnlyList<Clip> clips, PowerPasteDirection direction, bool explodeMode = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Advances to the next clip in the PowerPaste sequence.
    /// Called when a paste operation is detected.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AdvanceToNextAsync();

    /// <summary>
    /// Stops the current PowerPaste sequence.
    /// </summary>
    void Stop();

    /// <summary>
    /// Gets the current clip in the sequence.
    /// </summary>
    /// <returns>The current clip, or null if no sequence is active.</returns>
    Clip? GetCurrentClip();
}

/// <summary>
/// Represents the state of PowerPaste.
/// </summary>
public enum PowerPasteState
{
    /// <summary>
    /// PowerPaste is not active.
    /// </summary>
    Inactive,

    /// <summary>
    /// PowerPaste is active and waiting for paste operations.
    /// </summary>
    Active
}

/// <summary>
/// Represents the direction of PowerPaste traversal.
/// </summary>
public enum PowerPasteDirection
{
    /// <summary>
    /// Traverse upward (oldest to newest).
    /// </summary>
    Up,

    /// <summary>
    /// Traverse downward (newest to oldest).
    /// </summary>
    Down
}

/// <summary>
/// Event args for PowerPaste state changes.
/// </summary>
public class PowerPasteStateChangedEventArgs : EventArgs
{
    public PowerPasteState OldState { get; init; }
    public PowerPasteState NewState { get; init; }
    public PowerPasteDirection Direction { get; init; }
    public int TotalCount { get; init; }
}

/// <summary>
/// Event args for PowerPaste position changes.
/// </summary>
public class PowerPastePositionChangedEventArgs : EventArgs
{
    public int Position { get; init; }
    public int TotalCount { get; init; }
    public Clip? CurrentClip { get; init; }
    public bool IsComplete { get; init; }
}
