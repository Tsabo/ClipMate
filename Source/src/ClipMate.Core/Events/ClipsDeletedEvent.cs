namespace ClipMate.Core.Events;

/// <summary>
/// Event published when clips have been successfully deleted.
/// Allows UI to remove clips from the collection without a full reload.
/// </summary>
public record ClipsDeletedEvent(IReadOnlyList<Guid> DeletedClipIds);
