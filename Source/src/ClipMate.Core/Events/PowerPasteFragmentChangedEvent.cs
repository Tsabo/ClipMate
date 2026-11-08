namespace ClipMate.Core.Events;

/// <summary>
/// The Fragment Power Paste will paste next changed, or there is none: a whole clip is waiting, or Power Paste stopped.
/// Views show the Fragment where it lies in its clip's text, as ClipMate 7 selects it in its editor.
/// </summary>
/// <param name="ClipId">The exploded clip, or <c>null</c> when no Fragment is waiting.</param>
/// <param name="Text">The Fragment as it will be pasted.</param>
/// <param name="Start">Where the Fragment starts in the clip's text, in characters.</param>
/// <param name="Length">How many characters of the clip's text the Fragment covers.</param>
public sealed record PowerPasteFragmentChangedEvent(Guid? ClipId, string Text, int Start, int Length)
{
    /// <summary>
    /// No Fragment is waiting.
    /// </summary>
    public static PowerPasteFragmentChangedEvent None { get; } = new(null, string.Empty, 0, 0);

    /// <summary>
    /// The Fragment as a title shows it, in ClipMate 7's words: "Fragment [text]", on one line.
    /// </summary>
    public string Title => $"Fragment [{string.Join(' ', Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))}]";
}
