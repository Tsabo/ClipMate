namespace ClipMate.Core.Models;

/// <summary>
/// Defines the types of content that can be stored in a clip.
/// </summary>
public enum ClipType
{
    /// <summary>
    /// Plain text content.
    /// </summary>
    Text = 0,

    /// <summary>
    /// Rich Text Format content.
    /// </summary>
    RichText = 1,

    /// <summary>
    /// HTML formatted content.
    /// </summary>
    Html = 2,

    /// <summary>
    /// Image content (bitmap, PNG, JPEG, etc.).
    /// </summary>
    Image = 3,

    /// <summary>
    /// File paths or file drop list.
    /// </summary>
    Files = 4,

    /// <summary>
    /// Custom or unknown clipboard format.
    /// </summary>
    Custom = 5
}
