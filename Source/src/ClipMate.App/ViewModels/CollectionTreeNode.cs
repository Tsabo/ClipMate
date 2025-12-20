using ClipMate.Core.Models;

namespace ClipMate.App.ViewModels;

/// <summary>
/// Represents a collection node in the tree view.
/// </summary>
public class CollectionTreeNode : TreeNodeBase
{
    public CollectionTreeNode(Collection collection)
    {
        Collection = collection ?? throw new ArgumentNullException(nameof(collection));
        SortKey = collection.SortKey;
    }

    /// <summary>
    /// The underlying collection model.
    /// </summary>
    public Collection Collection { get; }

    public override string Name => Collection.Name;

    public override string Icon
    {
        get
        {
            // Special collections have distinct icons
            if (Collection.IsSpecial)
            {
                return Collection.Name.ToLowerInvariant() switch
                {
                    var name when name.Contains("trash") => "🗑️",
                    var name when name.Contains("search") => "🔍",
                    var _ => "⭐",
                };
            }

            // Match ClipMate 7.5 collection icons based on common names
            return Collection.Name.ToLowerInvariant() switch
            {
                "inbox" => "📥",
                "safe" => "🔒",
                "overflow" => "🌊",
                "samples" => "📋",
                var _ => Collection.Icon ?? "📁",
            };
        }
    }

    public override TreeNodeType NodeType =>
        Collection.IsSpecial
            ? TreeNodeType.SpecialCollection
            : TreeNodeType.Collection;

    /// <summary>
    /// Indicates if this collection rejects new clips (shown in red).
    /// </summary>
    public bool IsReadOnly => Collection.IsReadOnly;

    /// <summary>
    /// Indicates if this collection is "safe" (never auto-purges, shown underlined).
    /// </summary>
    public bool IsSafe => Collection.PurgePolicy == PurgePolicy.Never;
}
