namespace ClipMate.Core.Events;

/// <summary>
/// Message sent when a clip's properties (title, shortcut, etc.) are updated.
/// Used to notify UI components to refresh their display.
/// </summary>
public sealed class ClipUpdatedMessage
{
    public ClipUpdatedMessage(Guid clipId, string? title)
    {
        ClipId = clipId;
        Title = title;
    }

    /// <summary>
    /// The ID of the clip that was updated.
    /// </summary>
    public Guid ClipId { get; }

    /// <summary>
    /// The updated title of the clip.
    /// </summary>
    public string? Title { get; }
}
