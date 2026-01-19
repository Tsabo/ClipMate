namespace ClipMate.Core.Events;

/// <summary>
/// Message sent when a temporarily decrypted clip's cache expires.
/// ClipListViewModel should handle this to update the UI (icon, IsDecrypted flag).
/// </summary>
/// <param name="ClipId">The ID of the clip whose cache expired</param>
public record ClipCacheExpiredMessage(Guid ClipId);
