namespace ClipMate.Core.Models;

/// <summary>
/// Represents a single clipboard entry with metadata and relationships.
/// Content is stored in BLOB tables (BlobTxt, BlobPng, BlobJpg, BlobBlob) via ClipData entries.
/// Schema matches ClipMate 7.5 CLIP table structure.
/// </summary>
public partial class Clip
{
    /// <summary>
    /// Unique identifier for the clip (CLIP_GUID in ClipMate 7.5).
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Foreign key to the collection this clip belongs to (COLL_GUID).
    /// </summary>
    public Guid? CollectionId { get; set; }

    /// <summary>
    /// Foreign key to the folder within a collection (optional).
    /// </summary>
    public Guid? FolderId { get; set; }

    /// <summary>
    /// Foreign key to the user who created this clip (USER_ID).
    /// </summary>
    public int? UserId { get; set; }

    // ==================== ClipMate 7.5 Metadata Fields ====================

    /// <summary>
    /// Title of the clip - first line of text or custom title (TITLE, 60 chars max).
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Username/workstation that created this clip (CREATOR, 60 chars max).
    /// </summary>
    public string? Creator { get; set; }

    /// <summary>
    /// Timestamp when the clip was captured (TIMESTAMP).
    /// </summary>
    public DateTime CapturedAt { get; set; }

    /// <summary>
    /// Manual sort order for user-defined ordering (SORTKEY).
    /// Auto-generated as ID * 100 to allow manual re-ordering.
    /// </summary>
    public int SortKey { get; set; }

    /// <summary>
    /// Source URL if captured from browser (SOURCEURL, 250 chars max).
    /// </summary>
    public string? SourceUrl { get; set; }

    /// <summary>
    /// Whether user has customized the title (CUSTOMTITLE).
    /// </summary>
    public bool CustomTitle { get; set; }

    /// <summary>
    /// Language/locale setting (LOCALE).
    /// </summary>
    public int Locale { get; set; }

    /// <summary>
    /// Text wrapping preference (WRAPCHECK).
    /// </summary>
    public bool WrapCheck { get; set; }

    /// <summary>
    /// Whether content is encrypted (ENCRYPTED).
    /// </summary>
    public bool Encrypted { get; set; }

    /// <summary>
    /// Icon index for display (ICONS).
    /// </summary>
    public int Icons { get; set; }

    /// <summary>
    /// Soft delete flag (DEL).
    /// </summary>
    public bool Del { get; set; }

    /// <summary>
    /// Total size in bytes of all formats combined (SIZE).
    /// </summary>
    public int Size { get; set; }

    /// <summary>
    /// Timestamp when deleted (DELDATE).
    /// </summary>
    public DateTime? DelDate { get; set; }

    /// <summary>
    /// Checksum for duplicate detection (CHECKSUM).
    /// Integer hash of ContentHash for ClipMate 7.5 compatibility.
    /// </summary>
    public int Checksum { get; set; }

    /// <summary>
    /// Which tab to show by default (VIEWTAB): 0=Text, 1=RichText, 2=HTML.
    /// </summary>
    public int ViewTab { get; set; }

    /// <summary>
    /// Whether this is a keystroke macro (MACRO).
    /// </summary>
    public bool Macro { get; set; }

    /// <summary>
    /// Timestamp of last modification (LASTMODIFIED).
    /// </summary>
    public DateTime? LastModified { get; set; }

    // ==================== Additional Modern Fields ====================

    /// <summary>
    /// SHA256 hash of content for robust duplicate detection.
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
    /// Type of content (Text, Image, Files, etc.).
    /// Derived from ClipData format entries.
    /// </summary>
    public ClipType Type { get; set; }

    // ==================== Navigation Properties ====================
    // These are loaded on-demand when content is needed

    /// <summary>
    /// ClipData entries describing available formats for this clip.
    /// Each format has associated BLOB data in BlobTxt/BlobPng/etc.
    /// </summary>
    public ICollection<ClipData>? ClipDataFormats { get; set; }

    // ==================== Transient Properties (Not Stored) ====================
    // These are populated on-demand from BLOB tables via ClipData

    /// <summary>
    /// Plain text content (loaded from BlobTxt when needed).
    /// NOT stored in Clips table.
    /// </summary>
    public string? TextContent { get; set; }

    /// <summary>
    /// Rich text format content (loaded from BlobTxt when needed).
    /// NOT stored in Clips table.
    /// </summary>
    public string? RtfContent { get; set; }

    /// <summary>
    /// HTML formatted content (loaded from BlobTxt when needed).
    /// NOT stored in Clips table.
    /// </summary>
    public string? HtmlContent { get; set; }

    /// <summary>
    /// Binary image data (loaded from BlobPng/BlobJpg when needed).
    /// NOT stored in Clips table.
    /// </summary>
    public byte[]? ImageData { get; set; }

    /// <summary>
    /// File paths JSON (loaded from BlobBlob when needed).
    /// NOT stored in Clips table.
    /// </summary>
    public string? FilePathsJson { get; set; }

}
