namespace ClipMate.Core.Models;

/// <summary>
/// Represents a text template with variable placeholders for quick text insertion.
/// </summary>
public class Template
{
    /// <summary>
    /// Unique identifier for the template.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Display name of the template.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Template content with {variable} placeholders.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the template's purpose.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Foreign key to the collection this template belongs to (optional).
    /// </summary>
    public Guid? CollectionId { get; set; }

    /// <summary>
    /// Sort order for displaying templates.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Number of times this template has been used.
    /// </summary>
    public int UseCount { get; set; }

    /// <summary>
    /// Timestamp when the template was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp of last modification.
    /// </summary>
    public DateTime? ModifiedAt { get; set; }

    /// <summary>
    /// Timestamp of last use.
    /// </summary>
    public DateTime? LastUsedAt { get; set; }
}
