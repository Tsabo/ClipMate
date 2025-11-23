using ClipMate.Core.Models;

namespace ClipMate.App.ViewModels;

/// <summary>
/// Represents a virtual collection (pre-defined search query).
/// </summary>
public partial class VirtualCollectionTreeNode : TreeNodeBase
{
    /// <summary>
    /// The underlying virtual collection/saved search.
    /// </summary>
    public Collection VirtualCollection { get; }

    public override string Name => VirtualCollection.Name;

    public override string Icon
    {
        get
        {
            // Virtual collections have specific icons based on their purpose
            return VirtualCollection.Name.ToLowerInvariant() switch
            {
                "today" => "📅",
                "this week" => "📆",
                "this month" => "🗓️",
                "everything" => "🌐",
                var name when name.Contains("bitmap") || name.Contains("image") => "🖼️",
                "keystrokes macros" or "macros" => "⌨️",
                "since last import" => "📥",
                "since last export" => "📤",
                _ => "🔍"
            };
        }
    }

    public override TreeNodeType NodeType => TreeNodeType.VirtualCollection;

    /// <summary>
    /// SQL query for this virtual collection (if applicable).
    /// </summary>
    public string? SqlQuery => VirtualCollection.VirtualCollectionQuery;

    public VirtualCollectionTreeNode(Collection virtualCollection)
    {
        if (virtualCollection == null)
        {
            throw new ArgumentNullException(nameof(virtualCollection));
        }
        
        if (!virtualCollection.IsVirtual)
        {
            throw new ArgumentException("Collection must be virtual", nameof(virtualCollection));
        }

        VirtualCollection = virtualCollection;
        SortKey = virtualCollection.SortKey;
    }
}