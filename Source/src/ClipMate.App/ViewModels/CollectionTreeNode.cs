using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ClipMate.Core.Models;

namespace ClipMate.App.ViewModels;

/// <summary>
/// Base class for all tree nodes in the collection tree hierarchy.
/// Supports Database -> Collections -> Virtual Collections structure.
/// </summary>
public abstract partial class TreeNodeBase : ObservableObject
{
    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private TreeNodeBase? _parent;

    /// <summary>
    /// Display name for the node.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Icon identifier for the node (emoji or DevExpress icon).
    /// </summary>
    public virtual string Icon => "📁";

    /// <summary>
    /// Type of node for template selection and behavior.
    /// </summary>
    public abstract TreeNodeType NodeType { get; }

    /// <summary>
    /// Child nodes of this tree node.
    /// </summary>
    public ObservableCollection<TreeNodeBase> Children { get; } = new();

    /// <summary>
    /// Optional: Sort key for manual sorting (ClipMate 7.5 feature).
    /// </summary>
    public int SortKey { get; set; }
}

/// <summary>
/// Type of tree node in the collection hierarchy.
/// </summary>
public enum TreeNodeType
{
    /// <summary>
    /// Database root node (e.g., "My Clips").
    /// </summary>
    Database,

    /// <summary>
    /// Regular collection node (e.g., "InBox", "Safe", "Important Stuff").
    /// </summary>
    Collection,

    /// <summary>
    /// Folder within a collection.
    /// </summary>
    Folder,

    /// <summary>
    /// Virtual collections container node.
    /// </summary>
    VirtualCollectionsContainer,

    /// <summary>
    /// Virtual collection (pre-defined search, e.g., "Today", "Bitmaps").
    /// </summary>
    VirtualCollection,

    /// <summary>
    /// Special collection (e.g., "Trash Can", "Search Results").
    /// </summary>
    SpecialCollection
}

/// <summary>
/// Represents a database root node in the tree (e.g., "My Clips").
/// </summary>
public partial class DatabaseTreeNode : TreeNodeBase
{
    /// <summary>
    /// Database connection string or identifier.
    /// </summary>
    public string DatabasePath { get; }

    public override string Name { get; }

    public override string Icon => "💾"; // Database icon

    public override TreeNodeType NodeType => TreeNodeType.Database;

    public DatabaseTreeNode(string name, string databasePath)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        DatabasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
    }
}

/// <summary>
/// Represents a collection node in the tree view.
/// </summary>
public partial class CollectionTreeNode : TreeNodeBase
{
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
                    _ => "⭐"
                };
            }
            // Regular collections use folder icon or custom icon
            return Collection.Icon ?? "📁";
        }
    }

    public override TreeNodeType NodeType =>
        Collection.IsSpecial ? TreeNodeType.SpecialCollection : TreeNodeType.Collection;

    /// <summary>
    /// Indicates if this collection rejects new clips (shown in red).
    /// </summary>
    public bool IsReadOnly => Collection.IsReadOnly;

    /// <summary>
    /// Indicates if this collection is "safe" (never auto-purges, shown underlined).
    /// </summary>
    public bool IsSafe => Collection.PurgePolicy == PurgePolicy.Never;

    public CollectionTreeNode(Collection collection)
    {
        Collection = collection ?? throw new ArgumentNullException(nameof(collection));
        SortKey = collection.SortKey;
    }
}

/// <summary>
/// Represents a folder node in the tree view.
/// </summary>
public partial class FolderTreeNode : TreeNodeBase
{
    /// <summary>
    /// The underlying folder model.
    /// </summary>
    public Folder Folder { get; }

    public override string Name => Folder.Name;

    public override string Icon => "📂"; // Folder icon

    public override TreeNodeType NodeType => TreeNodeType.Folder;

    public FolderTreeNode(Folder folder)
    {
        Folder = folder ?? throw new ArgumentNullException(nameof(folder));
    }
}

/// <summary>
/// Container node for all virtual collections.
/// </summary>
public partial class VirtualCollectionsContainerNode : TreeNodeBase
{
    public override string Name => "Virtual";

    public override string Icon => "🔮"; // Crystal ball for virtual

    public override TreeNodeType NodeType => TreeNodeType.VirtualCollectionsContainer;

    public VirtualCollectionsContainerNode()
    {
        IsExpanded = false; // Collapsed by default
    }
}

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
                var name when name.Contains("bitmap") || name.Contains("image") => "🖼️",
                "macros" => "⌨️",
                var name when name.Contains("import") => "📥",
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
