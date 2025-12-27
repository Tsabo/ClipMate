namespace ClipMate.Core.Models;

/// <summary>
/// Represents a file-based template for merging clip data with placeholders.
/// Templates are plain text files stored in the Templates directory.
/// Uses #TAG# syntax matching ClipMate 7.5 format.
/// </summary>
public class FileTemplate
{
    /// <summary>
    /// Gets or sets the template name (derived from filename without extension).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the full file path to the template file.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the template content with placeholders (#TAG#).
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the last modified timestamp of the template file.
    /// Used to detect external changes for reloading.
    /// </summary>
    public DateTimeOffset LastModified { get; set; }
}
