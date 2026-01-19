namespace ClipMate.Core.Events;

/// <summary>
/// Request to decrypt selected clips with encryption key dialog.
/// </summary>
public record DecryptClipsRequestedEvent(IReadOnlyList<Guid> ClipIds);
