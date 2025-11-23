using ClipMate.Core.Models;

namespace ClipMate.App.ViewModels;

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

    public override string Icon => "📁"; // Folder icon

    public override TreeNodeType NodeType => TreeNodeType.Folder;

    public FolderTreeNode(Folder folder)
    {
        Folder = folder ?? throw new ArgumentNullException(nameof(folder));
    }
}