namespace ClipMate.Core.Events;

/// <summary>
/// Event raised when a request is made to show the ClipBar popup window.
/// Can be triggered by tray icon, taskbar icon, or hotkey.
/// </summary>
public sealed record ShowClipBarRequestedEvent;
