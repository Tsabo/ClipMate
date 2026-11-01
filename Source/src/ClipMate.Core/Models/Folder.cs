namespace ClipMate.Core.Models;

/// <summary>
/// Represents a folder within a collection for hierarchical organization.
/// </summary>
public class Folder
{
    /// <summary>
    /// Unique identifier for the folder.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Display name of the folder.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key to the parent collection.
    /// </summary>
    public Guid CollectionId { get; set; }

    /// <summary>
    /// Foreign key to the parent folder (null for root-level folders).
    /// </summary>
    public Guid? ParentFolderId { get; set; }

    /// <summary>
    /// Sort order for displaying folders in the tree view.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Timestamp when the folder was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp of last modification.
    /// </summary>
    public DateTime? ModifiedAt { get; set; }

    /// <summary>
    /// Whether this is a system-defined folder (Inbox, Safe, Trash, etc.) that cannot be deleted.
    /// </summary>
    public bool IsSystemFolder { get; set; }

    /// <summary>
    /// The type of folder, which defines its special behavior and rules.
    /// </summary>
    public FolderType FolderType { get; set; } = FolderType.Normal;

    /// <summary>
    /// Icon identifier for displaying in the tree view (optional).
    /// </summary>
    public string? IconName { get; set; }

    /// <summary>
    /// The folder a collection row with the Folder type and a parent is: folders are stored as collection rows.
    /// </summary>
    public static Folder FromCollection(Collection collection)
    {
        ArgumentNullException.ThrowIfNull(collection);
        return new Folder
        {
            Id = collection.Id,
            Name = collection.Title,
            CollectionId = collection.ParentId ?? Guid.Empty,
            ParentFolderId = collection.ParentId,
            SortOrder = collection.SortKey,
            CreatedAt = collection.CreatedAt,
            ModifiedAt = collection.LastUpdateTime,
            IsSystemFolder = false, // Could be derived from specific GUIDs
            FolderType = FolderType.Normal, // Default
            IconName = collection.IlIndex.ToString(),
        };
    }
}
