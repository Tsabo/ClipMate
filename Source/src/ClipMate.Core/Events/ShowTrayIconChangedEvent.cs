namespace ClipMate.Core.Events;

/// <summary>
/// Event raised when the ShowTrayIcon configuration setting changes.
/// </summary>
public sealed record ShowTrayIconChangedEvent(bool ShowTrayIcon);
