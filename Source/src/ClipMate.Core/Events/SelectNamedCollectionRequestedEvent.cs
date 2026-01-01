namespace ClipMate.Core.Events;

/// <summary>
/// Request to select a collection by name (Inbox, Safe, Overflow, Trash, etc.).
/// Used by Classic window quick-access buttons.
/// </summary>
/// <param name="CollectionName">The target collection name (e.g., "Inbox", "Safe", "Overflow", "Trash").</param>
public record SelectNamedCollectionRequestedEvent(string CollectionName);
