namespace ClipMate.Core.Events;

/// <summary>
/// Event for updating status bar text in the UI.
/// Allows background services and coordinators to communicate status to the UI layer.
/// </summary>
/// <param name="Message">The status message to display.</param>
/// <param name="IsError">Whether this is an error message.</param>
public record StatusUpdateEvent(string Message, bool IsError = false);
