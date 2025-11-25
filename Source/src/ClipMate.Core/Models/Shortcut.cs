namespace ClipMate.Core.Models;

/// <summary>
/// PowerPaste shortcut/nickname for quick clip access.
/// Matches ClipMate 7.5 shortcut table structure exactly.
/// </summary>
public class Shortcut
{
    /// <summary>
    /// Unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Foreign key to Clip (CLIP_ID in ClipMate 7.5).
    /// </summary>
    public Guid ClipId { get; set; }

    /// <summary>
    /// Nickname/shortcut text (NICKNAME in ClipMate 7.5, 64 chars max).
    /// Examples: ".sig", ".addr", ".s.welcome"
    /// Typically starts with a dot (.) for PowerPaste recognition.
    /// </summary>
    public string Nickname { get; set; } = string.Empty;

    /// <summary>
    /// Denormalized Clip GUID for performance (CLIP_GUID in ClipMate 7.5).
    /// Allows quick lookup without joining to Clip table.
    /// </summary>
    public Guid ClipGuid { get; set; }

    // Navigation property
    public Clip? Clip { get; set; }
}
