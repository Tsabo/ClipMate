namespace ClipMate.Core.Events;

/// <summary>
/// Asks the clip list to select a clip without copying it to the clipboard, such as when the hotkey navigation popup
/// is clicked. Does nothing if the clip is not in the list.
/// </summary>
/// <param name="ClipId">The clip to select.</param>
public sealed record SelectClipRequestedEvent(Guid ClipId);
