namespace ClipMate.Core.Events;

/// <summary>
/// Request to rename a clip with a dialog.
/// </summary>
public record RenameClipRequestedEvent(Guid ClipId, string CurrentTitle);
