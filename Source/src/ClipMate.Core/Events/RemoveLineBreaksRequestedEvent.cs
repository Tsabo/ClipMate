namespace ClipMate.Core.Events;

/// <summary>
/// Request to remove line breaks from the selected clips' text.
/// </summary>
/// <param name="RemoveAll">Removes the line breaks between paragraphs too.</param>
/// <param name="JoinWithoutSpaces">Joins lines with nothing between them, instead of with a space.</param>
public record RemoveLineBreaksRequestedEvent(bool RemoveAll = false, bool JoinWithoutSpaces = false);
