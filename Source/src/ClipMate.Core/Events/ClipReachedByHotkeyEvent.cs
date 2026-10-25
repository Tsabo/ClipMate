using ClipMate.Core.Models;

namespace ClipMate.Core.Events;

/// <summary>
/// Sent when the Scroll Next / Previous hotkeys moved the clip list to another clip, which becomes the Current Clip.
/// </summary>
/// <param name="Clip">The clip reached.</param>
/// <param name="DatabaseKey">The Database the clip is in (a Remote Database's routing key for a Peer's clip).</param>
public sealed record ClipReachedByHotkeyEvent(Clip Clip, string? DatabaseKey);
