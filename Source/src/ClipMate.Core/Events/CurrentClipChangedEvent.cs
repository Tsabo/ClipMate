using ClipMate.Core.Models;

namespace ClipMate.Core.Events;

/// <summary>
/// Messenger event sent when another clip becomes the Current Clip: ClipMate put it on the clipboard, Power Paste
/// armed it, or capture saved it (or found it as a Duplicate). May be sent on any thread.
/// </summary>
/// <param name="CurrentClip">The new Current Clip.</param>
public sealed record CurrentClipChangedEvent(CurrentClip CurrentClip);
