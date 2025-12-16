namespace ClipMate.Core.Events;

/// <summary>
/// Messenger event sent when image dimensions are loaded in the ClipViewerControl.
/// Allows the status bar to display pixel dimensions without coupling.
/// </summary>
public class ImageDimensionsLoadedEvent
{
    /// <summary>
    /// Creates a new image dimensions loaded event.
    /// </summary>
    /// <param name="clipId">The ID of the clip whose image was loaded.</param>
    /// <param name="width">The width of the image in pixels.</param>
    /// <param name="height">The height of the image in pixels.</param>
    public ImageDimensionsLoadedEvent(Guid clipId, int width, int height)
    {
        ClipId = clipId;
        Width = width;
        Height = height;
    }

    /// <summary>
    /// The ID of the clip whose image was loaded.
    /// </summary>
    public Guid ClipId { get; }

    /// <summary>
    /// The width of the image in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// The height of the image in pixels.
    /// </summary>
    public int Height { get; }
}
