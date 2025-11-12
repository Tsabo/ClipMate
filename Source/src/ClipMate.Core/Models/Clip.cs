namespace ClipMate.Core.Models;

/// <summary>
/// Represents a single clipboard entry with content, metadata, and relationships.
/// </summary>
public class Clip
{
    /// <summary>
    /// Unique identifier for the clip.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Type of content stored in this clip.
    /// </summary>
    public ClipType Type { get; set; }

    /// <summary>
    /// Plaintext content (always stored for indexing/searching).
    /// </summary>
    public string? TextContent { get; set; }

    /// <summary>
    /// Rich text format content (if Type is RichText).
    /// </summary>
    public string? RtfContent { get; set; }

    /// <summary>
    /// HTML formatted content (if Type is Html).
    /// </summary>
    public string? HtmlContent { get; set; }

    /// <summary>
    /// Binary image data (if Type is Image).
    /// </summary>
    public byte[]? ImageData { get; set; }

    /// <summary>
    /// File paths (if Type is Files), stored as JSON array.
    /// </summary>
    public string? FilePathsJson { get; set; }

    /// <summary>
    /// SHA256 hash of content for duplicate detection.
    /// </summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>
    /// Process name of the application that created this clip.
    /// </summary>
    public string? SourceApplicationName { get; set; }

    /// <summary>
    /// Window title of the application that created this clip.
    /// </summary>
    public string? SourceApplicationTitle { get; set; }

    /// <summary>
    /// Timestamp when the clip was captured.
    /// </summary>
    public DateTime CapturedAt { get; set; }

    /// <summary>
    /// Timestamp of last modification (for favoriting, labeling, etc.).
    /// </summary>
    public DateTime? ModifiedAt { get; set; }

    /// <summary>
    /// Timestamp of last access (for usage tracking).
    /// </summary>
    public DateTime? LastAccessedAt { get; set; }

    /// <summary>
    /// Number of times this clip has been pasted.
    /// </summary>
    public int PasteCount { get; set; }

    /// <summary>
    /// Whether this clip is marked as a favorite.
    /// </summary>
    public bool IsFavorite { get; set; }

    /// <summary>
    /// Optional label/tag for categorization.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Foreign key to the collection this clip belongs to.
    /// </summary>
    public Guid? CollectionId { get; set; }

    /// <summary>
    /// Foreign key to the folder within a collection (optional).
    /// </summary>
    public Guid? FolderId { get; set; }
}
