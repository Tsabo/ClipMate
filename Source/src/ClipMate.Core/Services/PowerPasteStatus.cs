namespace ClipMate.Core.Services;

/// <summary>
/// Whether Power Paste is running, for code that only needs to know that. <see cref="IPowerPaste" /> keeps it
/// up to date.
/// </summary>
public sealed class PowerPasteStatus
{
    /// <summary>
    /// True while a Power Paste sequence is active and holds the clipboard.
    /// </summary>
    public bool IsActive { get; set; }
}
