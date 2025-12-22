using ClipMate.Core.Models;

namespace ClipMate.App.ViewModels;

/// <summary>
/// Virtual collection that shows all clips where Del=true (soft-deleted clips) across all collections/databases.
/// This is the "Trashcan" view that aggregates deleted clips for review/restoration.
/// </summary>
public class TrashcanVirtualCollectionNode : TreeNodeBase
{
    public TrashcanVirtualCollectionNode(string databasePath)
    {
        DatabasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
    }

    /// <summary>
    /// The database path for loading clips from this virtual collection.
    /// </summary>
    public string DatabasePath { get; }

    /// <summary>
    /// The underlying collection model representing the trashcan. Used for metadata only.
    /// </summary>
    public Collection Collection { get; } = new()
    {
        AcceptNewClips = false,
        RetentionLimit = 200,
    };

    public override string Name => "Trashcan";

    public override string Icon => "🗑️"; // Trashcan icon

    public override TreeNodeType NodeType => TreeNodeType.VirtualCollection;

    /// <summary>
    /// Virtual ID for the trashcan (not a real collection in the database).
    /// </summary>
    public Guid VirtualId { get; } = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>
    /// Trashcan is read-only (rejects new clips from clipboard).
    /// This property makes the node appear in red, matching ClipMate 7.5 behavior.
    /// </summary>
    public bool IsReadOnly => true;
}
