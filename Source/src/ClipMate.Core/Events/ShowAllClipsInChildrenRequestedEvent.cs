namespace ClipMate.Core.Events;

/// <summary>
/// Event sent when user requests to show all clips in a collection and all its child folders recursively.
/// </summary>
public record ShowAllClipsInChildrenRequestedEvent(Guid CollectionId, string DatabaseKey);
