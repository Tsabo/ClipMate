namespace ClipMate.App.ViewModels;

/// <summary>
/// Container node for all virtual collections.
/// </summary>
public partial class VirtualCollectionsContainerNode : TreeNodeBase
{
    public override string Name => "Virtual";

    public override string Icon => "📁"; // Folder icon for Virtual container

    public override TreeNodeType NodeType => TreeNodeType.VirtualCollectionsContainer;

    public VirtualCollectionsContainerNode()
    {
        IsExpanded = false; // Collapsed by default
    }
}