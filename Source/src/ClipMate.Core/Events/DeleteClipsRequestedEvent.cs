using ClipMate.Core.Models;

namespace ClipMate.Core.Events;

/// <summary>
/// Request to delete selected clips with confirmation.
/// </summary>
public record DeleteClipsRequestedEvent(IReadOnlyList<Guid> ClipIds);
