namespace ClipMate.Core.Models;

/// <summary>
///     Partial class for Clip containing display-related computed properties.
///     These properties are used for UI binding in the ClipMate DataGrid.
/// </summary>
public partial class Clip
{
    /// <summary>
    ///     Gets the display title for the clip.
    ///     Returns the custom title if set, otherwise first 50 characters of text content.
    ///     Matches ClipMate 7.5 behavior.
    /// </summary>
    public string DisplayTitle
    {
        get
        {
            // If user has set a custom title, use that
            if (CustomTitle && !string.IsNullOrWhiteSpace(Title))
                return Title;

            // For text-based clips, use first 50 characters
            if (!string.IsNullOrWhiteSpace(TextContent))
            {
                var text = TextContent.Trim().Replace("\r\n", " ").Replace("\n", " ");

                return text.Length <= 50
                    ? text
                    : text[..50] + "...";
            }

            // For images
            if (Type == ClipType.Image)
                return "[Image]";

            // For files
            if (Type == ClipType.Files && !string.IsNullOrWhiteSpace(FilePathsJson))
                return "[Files]";

            // Fallback
            return Title ?? "[Empty Clip]";
        }
    }

    /// <summary>
    ///     Whether this clip has text format available (CF_TEXT/CF_UNICODETEXT).
    ///     Set during repository load operations. NOT stored in Clips table.
    /// </summary>
    public bool HasText { get; set; }

    /// <summary>
    ///     Whether this clip has RTF format available (CF_RTF).
    ///     Set during repository load operations. NOT stored in Clips table.
    /// </summary>
    public bool HasRtf { get; set; }

    /// <summary>
    ///     Whether this clip has HTML format available (HTML Format).
    ///     Set during repository load operations. NOT stored in Clips table.
    /// </summary>
    public bool HasHtml { get; set; }

    /// <summary>
    ///     Whether this clip has bitmap format available (CF_BITMAP/CF_DIB).
    ///     Set during repository load operations. NOT stored in Clips table.
    /// </summary>
    public bool HasBitmap { get; set; }

    /// <summary>
    ///     Whether this clip has file list format available (CF_HDROP).
    ///     Set during repository load operations. NOT stored in Clips table.
    /// </summary>
    public bool HasFiles { get; set; }

    /// <summary>
    ///     Gets whether this clip has a picture/metafile.
    /// </summary>
    public bool HasPicture => Type == ClipType.Image && ImageData != null;

    /// <summary>
    ///     Gets the source application display name (short name from full path if needed).
    /// </summary>
    public string SourceDisplay =>
        string.IsNullOrWhiteSpace(SourceApplicationName)
            ? string.Empty
            : Path.GetFileNameWithoutExtension(SourceApplicationName)?.ToUpperInvariant() ?? SourceApplicationName;

    /// <summary>
    ///     Gets formatted size string (bytes, K, M).
    /// </summary>
    public string SizeDisplay
    {
        get
        {
            if (Size < 1024)
                return Size.ToString();

            if (Size < 1024 * 1024)
                return $"{Size / 1024}K";

            return $"{Size / (1024 * 1024)}M";
        }
    }

    /// <summary>
    ///     Gets formatted username (Creator or UserId).
    /// </summary>
    public string UserDisplay => Creator ?? (UserId.HasValue
        ? $"User {UserId}"
        : string.Empty);

    /// <summary>
    ///     Gets the primary icon type for the first column.
    ///     Based on ClipMate 7.5 icon priority: Bitmap > Picture > RTF > HTML > Files > Text
    /// </summary>
    public string IconType
    {
        get
        {
            if (HasBitmap)
                return "Bitmap";

            if (HasPicture)
                return "Picture";

            if (HasRtf)
                return "RichText";

            if (HasHtml)
                return "HTML";

            if (HasFiles)
                return "Files";

            if (HasText)
                return "Text";

            return "Unknown";
        }
    }

    /// <summary>
    ///     Icon glyph string representing all available formats.
    ///     Set during repository load operations. NOT stored in Clips table.
    /// </summary>
    public string IconGlyph { get; set; } = "❓";
}
