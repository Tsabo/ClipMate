namespace ClipMate.Core.Events;

/// <summary>
/// Event to request scrolling to the previous clip in the list.
/// </summary>
/// <param name="FromHotkey">
/// Whether the Scroll Previous hotkey asked for it, rather than a command inside a ClipMate window; only then is the
/// clip reached shown in the hotkey navigation popup.
/// </param>
public sealed record SelectPreviousClipEvent(bool FromHotkey = false);
