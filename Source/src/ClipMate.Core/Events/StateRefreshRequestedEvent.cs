namespace ClipMate.Core.Events;

/// <summary>
/// Event sent when service state changes and ViewModels need to refresh their derived properties.
/// </summary>
public record StateRefreshRequestedEvent;
