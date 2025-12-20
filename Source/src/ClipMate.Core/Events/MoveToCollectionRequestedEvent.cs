namespace ClipMate.Core.Events;

/// <summary>
/// Request to move selected clips to a collection with dialog.
/// </summary>
public record MoveToCollectionRequestedEvent(IReadOnlyList<Guid> ClipIds);
