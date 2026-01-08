using ClipMate.Core.Models;

namespace ClipMate.App.Models.TreeNodes;

/// <summary>
/// Represents a folder node in the tree view.
/// </summary>
public class FolderTreeNode : TreeNodeBase
{
    public FolderTreeNode(Folder folder)
    {
        Folder = folder ?? throw new ArgumentNullException(nameof(folder));
    }

    /// <summary>
    /// The underlying folder model.
    /// </summary>
    public Folder Folder { get; }

    public override string Name => Folder.Name;

    public override string Icon => "📁"; // Folder icon

    public override TreeNodeType NodeType => TreeNodeType.Folder;
}
