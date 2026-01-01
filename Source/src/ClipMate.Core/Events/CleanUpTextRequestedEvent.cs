namespace ClipMate.Core.Events;

/// <summary>
/// Request to clean up text in the selected clip (remove extra whitespace, normalize line endings).
/// </summary>
public record CleanUpTextRequestedEvent;
