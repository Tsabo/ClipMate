namespace ClipMate.Core.Models.Configuration;

/// <summary>
/// Defines the default editor view type.
/// </summary>
public enum EditorViewType
{
    /// <summary>
    /// Plain text view.
    /// </summary>
    Text,

    /// <summary>
    /// Unicode text view (same as Text in Monaco).
    /// </summary>
    Unicode,

    /// <summary>
    /// Rich Text Format view.
    /// </summary>
    Rtf,

    /// <summary>
    /// Bitmap image view.
    /// </summary>
    Bitmap,

    /// <summary>
    /// Picture/image view.
    /// </summary>
    Picture,

    /// <summary>
    /// HTML view.
    /// </summary>
    Html,

    /// <summary>
    /// Binary/hex view.
    /// </summary>
    Binary
}