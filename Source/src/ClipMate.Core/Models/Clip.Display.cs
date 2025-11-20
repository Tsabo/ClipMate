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
    /// Gets the source application display name (short name from full path if needed).
    /// </summary>
    public string SourceDisplay => 
        string.IsNullOrWhiteSpace(SourceApplicationName) 
            ? string.Empty 
            : System.IO.Path.GetFileNameWithoutExtension(SourceApplicationName)?.ToUpperInvariant() ?? SourceApplicationName;

    /// <summary>
    /// Gets formatted size string (bytes, K, M).
    /// </summary>
    public string SizeDisplay
    {
        get
        {
            if (Size < 1024) return Size.ToString();
            if (Size < 1024 * 1024) return $"{Size / 1024}K";
            return $"{Size / (1024 * 1024)}M";
        }
    }

    /// <summary>
    /// Gets formatted user name (Creator or UserId).
    /// </summary>
    public string UserDisplay => Creator ?? (UserId.HasValue ? $"User {UserId}" : string.Empty);

    /// <summary>
    /// Gets the primary icon type for the first column.
    /// Based on ClipMate 7.5 icon priority: Bitmap > Picture > RTF > HTML > Files > Text
    /// </summary>
    public string IconType
    {
        get
        {
            if (HasBitmap) return "Bitmap";
            if (HasPicture) return "Picture";
            if (HasRtf) return "RichText";
            if (HasHtml) return "HTML";
            if (HasFiles) return "Files";
            if (HasText) return "Text";
            return "Unknown";
        }
    }

    /// <summary>
    /// Gets all applicable icon glyphs for the clip.
    /// Returns cached value if available (set during LoadFormatFlagsAsync),
    /// otherwise computes from current format flags.
    /// </summary>
    public string IconGlyph
    {
        get
        {
            // Return cached value if available
            if (!string.IsNullOrEmpty(CachedIconGlyph))
                return CachedIconGlyph;

            // Otherwise compute from Has* properties
            var icons = new System.Collections.Generic.List<string>();

            // Add icons for each format present
            // Order: Bitmap, Picture, RTF, HTML, Files, Text
            if (HasBitmap) icons.Add("🖼"); // Picture frame
            if (HasPicture && !HasBitmap) icons.Add("🎨"); // Artist palette (only if no bitmap)
            if (HasRtf) icons.Add("🅰"); // Letter A (formatted)
            if (HasHtml) icons.Add("🌐"); // Globe (web)
            if (HasFiles) icons.Add("📁"); // Folder
            if (HasText) icons.Add("📄"); // Document

            // Fallback: if no format flags loaded, use Type property
            if (icons.Count == 0)
            {
                icons.Add(Type switch
                {
                    ClipType.Text => "📄",
                    ClipType.Image => "🖼",
                    ClipType.Files => "📁",
                    _ => "❓"
                });
            }

            return string.Join("", icons);
        }
    }
}
