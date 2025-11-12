namespace ClipMate.Core.Models;

/// <summary>
/// Represents a named collection/database for organizing clips.
/// </summary>
public class Collection
{
    /// <summary>
    /// Unique identifier for the collection.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Display name of the collection.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the collection's purpose.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Sort order for displaying collections in the tree view.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Whether this collection is currently active/selected.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Timestamp when the collection was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp of last modification.
    /// </summary>
    public DateTime? ModifiedAt { get; set; }
}
