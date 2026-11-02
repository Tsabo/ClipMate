namespace ClipMate.Core.Services.Abstractions;

/// <summary>
/// Event args for Power Paste state changes.
/// </summary>
public class PowerPasteStateChangedEventArgs : EventArgs
{
    public PowerPasteState OldState { get; init; }
    public PowerPasteState NewState { get; init; }
    public PowerPasteDirection Direction { get; init; }
    public int TotalCount { get; init; }
}
