namespace ClipMate.Core.Events;

/// <summary>
/// Event for requesting the clip list to reload its contents.
/// Sent after clip operations (delete, create, move) that affect the current view.
/// </summary>
public record ReloadClipsRequestedEvent;
