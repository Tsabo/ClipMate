namespace ClipMate.Core.Models;

/// <summary>
/// Stores text content for clipboard formats.
/// Matches ClipMate 7.5 BLOBTXT table structure.
/// Used for: CF_TEXT, CF_UNICODETEXT, CF_HTML, CF_RTF.
/// </summary>
public class BlobTxt
{
    /// <summary>
    /// Unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Foreign key to ClipData (CLIPDATA_ID in ClipMate 7.5).
    /// </summary>
    public Guid ClipDataId { get; set; }

    /// <summary>
    /// Foreign key to Clip (CLIP_ID in ClipMate 7.5).
    /// Denormalized for query performance.
    /// </summary>
    public Guid ClipId { get; set; }

    /// <summary>
    /// Text content (DATA in ClipMate 7.5, stored as MEMO).
    /// </summary>
    public string Data { get; set; } = string.Empty;

    // Navigation property
    public ClipData? ClipData { get; set; }
}
