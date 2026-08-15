namespace ClipMate.Core.Events;

/// <summary>
/// Notifies that an existing clip's content was changed in place (e.g. Auto-Append mode merging
/// a new capture into the growing clip). Carries the updated field values so the recipient can
/// apply them to the same clip object instance bound to the UI before refreshing it.
/// </summary>
public sealed record ClipContentUpdatedEvent(Guid ClipId, string DatabaseKey, string TextContent, string Title, int Size);
