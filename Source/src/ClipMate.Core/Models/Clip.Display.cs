namespace ClipMate.Core.Models;

/// <summary>
/// Partial class for Clip containing display-related computed properties.
/// These properties are used for UI binding in the ClipMate DataGrid.
/// </summary>
public partial class Clip
{
    /// <summary>
    /// Gets the display title for the clip.
    /// Returns the custom title if set, otherwise first 50 characters of text content.
    /// Matches ClipMate 7.5 behavior.
    /// </summary>
    public string DisplayTitle
    {
        get
        {
            // If user has set a custom title, use that
            if (CustomTitle && !string.IsNullOrWhiteSpace(Title))
            {
                return Title;
            }

            // For text-based clips, use first 50 characters
            if (!string.IsNullOrWhiteSpace(TextContent))
            {
                var text = TextContent.Trim().Replace("\r\n", " ").Replace("\n", " ");
                return text.Length <= 50 ? text : text[..50] + "...";
            }

            // For images
            if (Type == ClipType.Image)
            {
                return "[Image]";
            }

            // For files
            if (Type == ClipType.Files && !string.IsNullOrWhiteSpace(FilePathsJson))
            {
                return "[Files]";
            }

            // Fallback
            return Title ?? "[Empty Clip]";
        }
    }

    /// <summary>
    /// Gets whether this clip has text format.
    /// </summary>
    public bool HasText => !string.IsNullOrWhiteSpace(TextContent);

    /// <summary>
    /// Gets whether this clip has RTF format.
    /// </summary>
    public bool HasRtf => !string.IsNullOrWhiteSpace(RtfContent);

    /// <summary>
    /// Gets whether this clip has HTML format.
    /// </summary>
    public bool HasHtml => !string.IsNullOrWhiteSpace(HtmlContent);

    /// <summary>
    /// Gets whether this clip has image data.
    /// </summary>
    public bool HasBitmap => ImageData != null && ImageData.Length > 0;

    /// <summary>
    /// Gets whether this clip has file paths.
    /// </summary>
    public bool HasFiles => !string.IsNullOrWhiteSpace(FilePathsJson);

    /// <summary>
    /// Gets whether this clip has a picture/metafile.
    /// </summary>
    public bool HasPicture => Type == ClipType.Image && ImageData != null;

    /// <summary>
    /// Gets the source application display name (from SourceApplicationName).
    /// </summary>
    public string SourceDisplay => SourceApplicationName ?? string.Empty;
}
