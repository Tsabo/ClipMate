namespace ClipMate.Core.Events;

/// <summary>
/// Event raised when the ShowTaskbarIcon configuration setting changes.
/// </summary>
public sealed record ShowTaskbarIconChangedEvent(bool ShowTaskbarIcon);
