namespace ClipMate.Core.Models;

/// <summary>
/// Represents metadata for a single clipboard format within a clip.
/// Matches ClipMate 7.5 ClipData table structure exactly.
/// </summary>
public class ClipData
{
    /// <summary>
    /// Unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Foreign key to parent Clip (CLIP_ID in ClipMate 7.5).
    /// </summary>
    public Guid ClipId { get; set; }

    /// <summary>
    /// Format name (e.g., "CF_TEXT", "CF_BITMAP", "CF_HTML", "CF_UNICODETEXT").
    /// 60 chars max (FORMAT_NAME in ClipMate 7.5).
    /// </summary>
    public string FormatName { get; set; } = string.Empty;

    /// <summary>
    /// Windows clipboard format code (FORMAT in ClipMate 7.5).
    /// Standard formats:
    /// - 1 = CF_TEXT
    /// - 2 = CF_BITMAP
    /// - 8 = CF_DIB
    /// - 13 = CF_UNICODETEXT
    /// - 49161 = CF_HTML (custom)
    /// </summary>
    public int Format { get; set; }

    /// <summary>
    /// Size of this format's data in bytes (SIZE in ClipMate 7.5).
    /// </summary>
    public int Size { get; set; }

    /// <summary>
    /// Storage type indicator (STORAGE_TYPE in ClipMate 7.5).
    /// Determines which BLOB table stores the data:
    /// - 1 = BLOBTXT (text formats: CF_TEXT, CF_UNICODETEXT, CF_HTML, CF_RTF)
    /// - 2 = BLOBJPG (JPEG images)
    /// - 3 = BLOBPNG (PNG images)
    /// - 4 = BLOBBLOB (other binary data: CF_BITMAP, CF_DIB, custom formats)
    /// </summary>
    public int StorageType { get; set; }

    // Navigation property
    public Clip? Clip { get; set; }
}
