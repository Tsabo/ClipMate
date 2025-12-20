namespace ClipMate.Core.Events;

/// <summary>
/// Request to create a new empty clip in the current collection.
/// </summary>
public record CreateNewClipRequestedEvent(Guid CollectionId);
