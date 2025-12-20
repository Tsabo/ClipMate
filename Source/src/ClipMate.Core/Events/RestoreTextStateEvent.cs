namespace ClipMate.Core.Events;

/// <summary>
/// Event to request restoring text editor state (undo operation).
/// Per user manual: undo applies only to text edits and toolbar transformations.
/// </summary>
public record RestoreTextStateEvent(string TextContent);
