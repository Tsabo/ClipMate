using ClipMate.Core.Models;

namespace ClipMate.Core.Services.Abstractions;

/// <summary>
/// Event args for Power Paste position changes.
/// </summary>
public class PowerPastePositionChangedEventArgs : EventArgs
{
    public int Position { get; init; }
    public int TotalCount { get; init; }
    public Clip? CurrentClip { get; init; }

    /// <summary>
    /// True when <see cref="CurrentClip" /> is one piece of an exploded clip, which is pasted as plain text only.
    /// </summary>
    public bool IsExplodePiece { get; init; }
}
