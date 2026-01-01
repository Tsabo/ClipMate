namespace ClipMate.Core.Events;

/// <summary>
/// Notifies that the collection tree needs to be refreshed (e.g., after database activation/deactivation).
/// </summary>
public record RefreshCollectionTreeRequestedEvent;
