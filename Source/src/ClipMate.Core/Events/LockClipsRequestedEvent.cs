namespace ClipMate.Core.Events;

/// <summary>
/// Event requesting that decrypted clips be locked (cleared from cache).
/// </summary>
/// <param name="ClipIds">List of specific clip IDs to lock. If empty and LockAll is false, locks selected clips.</param>
/// <param name="LockAll">If true, locks all cached clips and forgets encryption key. Takes precedence over ClipIds.</param>
public record LockClipsRequestedEvent(IReadOnlyList<Guid> ClipIds, bool LockAll = false);
