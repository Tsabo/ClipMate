namespace ClipMate.Core.Events;

/// <summary>
/// Request to copy selected clips to a collection with dialog.
/// </summary>
public record CopyToCollectionRequestedEvent(IReadOnlyList<Guid> ClipIds);
