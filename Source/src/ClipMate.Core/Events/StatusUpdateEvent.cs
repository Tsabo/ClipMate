namespace ClipMate.Core.Events;

/// <summary>
/// Event for updating status bar text in the UI.
/// Allows background services and coordinators to communicate status to the UI layer.
/// </summary>
/// <param name="Message">The status message to display.</param>
/// <param name="IsError">Whether this is an error message.</param>
public record StatusUpdateEvent(string Message, bool IsError = false)
{
    /// <summary>
    /// Gets whether the message stays while the clip list loads and selects its first clip itself, until the user
    /// picks a clip or node: an "All Databases" search's summary, sent after the tree selected a Search Results node.
    /// </summary>
    public bool KeepUntilUserSelects { get; init; }
}
