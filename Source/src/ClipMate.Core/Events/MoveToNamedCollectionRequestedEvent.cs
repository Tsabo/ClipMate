namespace ClipMate.Core.Events;

/// <summary>
/// Request to move selected clips to a collection by name (Inbox, Safe, Overflow, Trash, etc.).
/// Used by Classic window quick-access buttons.
/// </summary>
/// <param name="CollectionName">The target collection name (e.g., "Inbox", "Safe", "Overflow", "Trash").</param>
public record MoveToNamedCollectionRequestedEvent(string CollectionName);
