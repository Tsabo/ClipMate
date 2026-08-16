namespace ClipMate.Core.Events;

/// <summary>
/// Request to move the currently selected clip to the top or bottom of its collection's manual sort order.
/// </summary>
public record MoveClipSortOrderRequestedEvent(bool ToTop);
