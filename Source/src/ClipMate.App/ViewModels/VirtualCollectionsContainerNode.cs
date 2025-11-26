namespace ClipMate.App.ViewModels;

/// <summary>
/// Container node for all virtual collections.
/// </summary>
public class VirtualCollectionsContainerNode : TreeNodeBase
{
    public VirtualCollectionsContainerNode()
    {
        IsExpanded = false; // Collapsed by default
    }

    public override string Name => "Virtual";

    public override string Icon => "📁"; // Folder icon for Virtual container

    public override TreeNodeType NodeType => TreeNodeType.VirtualCollectionsContainer;
}
