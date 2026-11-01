namespace ClipMate.Core.Models;

/// <summary>
/// The Current Clip: the clip ClipMate last put on, or captured from, the clipboard.
/// </summary>
/// <param name="Clip">The clip, with its content as it went on the clipboard.</param>
/// <param name="DatabaseKey">The Database it is stored in; null for content that isn't a stored clip.</param>
public sealed record CurrentClip(Clip Clip, string? DatabaseKey);
