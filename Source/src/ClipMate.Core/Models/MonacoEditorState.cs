namespace ClipMate.Core.Models;

/// <summary>
/// Represents Monaco Editor state for a specific clip data format (typically CF_TEXT).
/// Stores language selection and view state (scroll position, cursor, selections, folding).
/// </summary>
public class MonacoEditorState
{
    /// <summary>
    /// Gets or sets the primary key.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the foreign key to ClipData (one-to-one relationship with CF_TEXT format).
    /// </summary>
    public Guid ClipDataId { get; set; }

    /// <summary>
    /// Gets or sets the Monaco language identifier (e.g., "javascript", "python", "plaintext").
    /// </summary>
    public string Language { get; set; } = "plaintext";

    /// <summary>
    /// Gets or sets the Monaco view state as JSON (scroll position, cursor, selections, folding).
    /// Null if no view state has been saved yet.
    /// </summary>
    public string? ViewState { get; set; }

    /// <summary>
    /// Gets or sets the last modified timestamp.
    /// </summary>
    public DateTime LastModified { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the associated ClipData.
    /// </summary>
    public ClipData? ClipData { get; set; }
}
