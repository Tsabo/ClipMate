namespace ClipMate.Core.Events;

/// <summary>
/// Event message sent when the QuickPaste toolbar's "Paste Now" action is triggered.
/// The ClipListView should handle this by pasting the currently selected clip.
/// </summary>
public sealed class QuickPasteNowEvent
{
}
