namespace ClipMate.Core.Models;

/// <summary>
/// Stores PNG image data.
/// Matches ClipMate 7.5 BLOBPNG table structure.
/// </summary>
public class BlobPng
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
    /// PNG binary data (DATA in ClipMate 7.5, stored as BLOB).
    /// </summary>
    public byte[] Data { get; set; } = [];

    // Navigation property
    public ClipData? ClipData { get; set; }
}
