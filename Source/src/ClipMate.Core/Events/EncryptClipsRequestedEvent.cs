namespace ClipMate.Core.Events;

/// <summary>
/// Request to encrypt selected clips with encryption key dialog.
/// </summary>
public record EncryptClipsRequestedEvent(IReadOnlyList<Guid> ClipIds);
