namespace ClipMate.Core.Models;

/// <summary>
/// Stores generic binary data for clipboard formats.
/// Matches ClipMate 7.5 BLOBBLOB table structure.
/// Used for: CF_BITMAP, CF_DIB, and other custom binary formats.
/// </summary>
public class BlobBlob
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
    /// Generic binary data (DATA in ClipMate 7.5, stored as BLOB).
    /// </summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();

    // Navigation property
    public ClipData? ClipData { get; set; }
}
