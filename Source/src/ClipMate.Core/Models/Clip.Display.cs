namespace ClipMate.Core.Models;

/// <summary>
/// Partial class for Clip containing display-related computed properties.
/// These properties are used for UI binding in the ClipMate DataGrid.
/// </summary>
public partial class Clip
{
    /// <summary>
    /// Gets the display title for the clip.
    /// Returns the stored Title field, computing a fallback only if Title is empty.
    /// Title is set at capture time and only updated when text is edited IF
    /// AutoChangeClipTitles setting is enabled AND CustomTitle flag is false.
    /// When CustomTitle=true (user manually renamed), title never auto-updates.
    /// </summary>
    public string DisplayTitle
    {
        get
        {
            // Return the stored Title if available
            if (!string.IsNullOrWhiteSpace(Title))
                return Title;

            // Fallback: compute from content only if Title is empty
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

            // Final fallback
            return "[Empty Clip]";
        }
    }

    /// <summary>
    /// Whether this clip has text format available (CF_TEXT/CF_UNICODETEXT).
    /// Set during repository load operations. NOT stored in Clips table.
    /// </summary>
    public bool HasText { get; set; }

    /// <summary>
    /// Whether this clip has RTF format available (CF_RTF).
    /// Set during repository load operations. NOT stored in Clips table.
    /// </summary>
    public bool HasRtf { get; set; }

    /// <summary>
    /// Whether this clip has HTML format available (HTML Format).
    /// Set during repository load operations. NOT stored in Clips table.
    /// </summary>
    public bool HasHtml { get; set; }

    /// <summary>
    /// Whether this clip has bitmap format available (CF_BITMAP/CF_DIB).
    /// Set during repository load operations. NOT stored in Clips table.
    /// </summary>
    public bool HasBitmap { get; set; }

    /// <summary>
    /// Whether this clip has file list format available (CF_HDROP).
    /// Set during repository load operations. NOT stored in Clips table.
    /// </summary>
    public bool HasFiles { get; set; }

    /// <summary>
    /// Gets whether this clip has a picture/metafile.
    /// </summary>
    public bool HasPicture => Type == ClipType.Image && ImageData != null;

    /// <summary>
    /// Gets the source application display name (short name from full path if needed).
    /// </summary>
    public string SourceDisplay =>
        string.IsNullOrWhiteSpace(SourceApplicationName)
            ? string.Empty
            : Path.GetFileNameWithoutExtension(SourceApplicationName)?.ToUpperInvariant() ?? SourceApplicationName;

    /// <summary>
    /// Gets formatted size string (bytes, K, M).
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
    /// Gets formatted username (Creator or UserId).
    /// </summary>
    public string UserDisplay => Creator ?? (UserId.HasValue
        ? $"User {UserId}"
        : string.Empty);

    /// <summary>
    /// Gets the primary icon type for the first column.
    /// Based on ClipMate 7.5 icon priority: Bitmap > Picture > RTF > HTML > Files > Text
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
    /// Icon glyph string representing all available formats.
    /// Set during repository load operations. NOT stored in Clips table.
    /// </summary>
    public string IconGlyph { get; set; } = "❓";

    /// <summary>
    /// Gets the CapturedAt timestamp formatted for display based on user preferences.
    /// By default, shows the time in the viewer's local timezone.
    /// Can be configured to show original captured timezone via ShowTimestampsInLocalTime setting.
    /// </summary>
    public DateTimeOffset CapturedAtDisplay
    {
        get
        {
            // TODO: Read ShowTimestampsInLocalTime from configuration service
            // For now, default to showing in local time (most common use case)
            var showInLocalTime = true;

            return showInLocalTime
                ? CapturedAt.ToLocalTime()
                : CapturedAt;
        }
    }
}
