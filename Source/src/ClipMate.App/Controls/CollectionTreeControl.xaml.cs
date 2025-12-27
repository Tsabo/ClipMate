using System.Diagnostics;
using ClipMate.App.ViewModels;
using ClipMate.Core.Models;
using DevExpress.Xpf.Core;
using DragDropEffects = System.Windows.DragDropEffects;

namespace ClipMate.App.Controls;

/// <summary>
/// Interaction logic for CollectionTreeControl.xaml
/// Displays hierarchical collection tree using DevExpress TreeListControl.
/// Structure: Database -> Collections/Virtual Collections -> Folders
/// </summary>
public partial class CollectionTreeControl
{
    public CollectionTreeControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Handles drag-over events to control where collections and clips can be dropped.
    /// Prevents dropping collections into virtual collections container or folders.
    /// Allows dropping clips onto any collection (except read-only ones).
    /// </summary>
    private void TreeView_DragRecordOver(object sender, DragRecordOverEventArgs e)
    {
        // Get the target node
        var targetNode = TreeView.GetNodeByRowHandle(e.TargetRowHandle);
        if (targetNode?.Content is not TreeNodeBase targetTreeNode)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        // Check if we're dragging clips (from ClipListView)
        if (e.Data.GetDataPresent(typeof(Clip[])))
        {
            // Allow dropping clips only onto CollectionTreeNode (not virtual containers, not folders)
            if (targetTreeNode is CollectionTreeNode collectionNode)
            {
                // Don't allow dropping into read-only or virtual collections
                if (collectionNode.Collection.ReadOnly || collectionNode.Collection.IsVirtual)
                    e.Effects = DragDropEffects.None;
                else
                    e.Effects = DragDropEffects.Move;
            }
            else
                e.Effects = DragDropEffects.None;

            e.Handled = true;
            return;
        }

        // Original collection reordering logic
        if (e.Data.GetData(typeof(RecordDragDropData)) is not RecordDragDropData dragData || dragData.Records.Length == 0)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        // Only allow dragging CollectionTreeNode objects
        var sourceNodes = dragData.Records.OfType<CollectionTreeNode>().ToList();
        if (sourceNodes.Count == 0 || sourceNodes.Count != dragData.Records.Length)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        // Prevent dropping into VirtualCollectionsContainerNode or FolderTreeNode
        if (targetTreeNode is VirtualCollectionsContainerNode or FolderTreeNode)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        // Prevent dropping virtual collections
        if (sourceNodes.Any(p => p.Collection.IsVirtual))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        // Only allow drop positions before/after, not inside (we're reordering, not nesting)
        if (e.DropPosition == DropPosition.Inside && targetTreeNode is not DatabaseTreeNode)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        // Allow the drop
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    /// <summary>
    /// Handles drop events to either reorder collections or move clips to a collection.
    /// </summary>
    private async void TreeView_DropRecord(object sender, DropRecordEventArgs e)
    {
        if (DataContext is not CollectionTreeViewModel viewModel)
            return;

        // Check if we're dropping clips (from ClipListView)
        if (e.Data.GetDataPresent(typeof(Clip[])))
        {
            if (e.Data.GetData(typeof(Clip[])) is not Clip[] clips || clips.Length == 0)
            {
                e.Handled = true;
                return;
            }

            // Get target collection node
            var clipTargetNode = TreeView.GetNodeByRowHandle(e.TargetRowHandle);
            if (clipTargetNode?.Content is not CollectionTreeNode clipTargetCollection)
            {
                e.Handled = true;
                return;
            }

            try
            {
                // Call ViewModel method to move clips to the target collection
                await viewModel.MoveClipsToCollectionAsync(
                    clips.Select(p => p.Id).ToList(),
                    clipTargetCollection.Collection.Id,
                    null); // DatabaseId will be determined by the ViewModel
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Move clips failed: {ex.Message}");
            }

            e.Handled = true;
            return;
        }

        // Original collection reordering logic
        if (e.Data.GetData(typeof(RecordDragDropData)) is not RecordDragDropData dragData)
        {
            e.Handled = true;
            return;
        }

        var droppedNodes = dragData.Records.OfType<CollectionTreeNode>().ToList();
        if (droppedNodes.Count == 0)
        {
            e.Handled = true;
            return;
        }

        // Get target node for collection reordering
        var targetNode = TreeView.GetNodeByRowHandle(e.TargetRowHandle);
        if (targetNode?.Content is not CollectionTreeNode targetCollectionNode)
        {
            e.Handled = true;
            return;
        }

        try
        {
            // Call ViewModel method to handle the reordering
            await viewModel.ReorderCollectionsAsync(
                droppedNodes.Select(p => p.Collection.Id).ToList(),
                targetCollectionNode.Collection.Id,
                e.DropPosition == DropPosition.After);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Drop failed: {ex.Message}");
        }

        e.Handled = true;
    }

    /// <summary>
    /// Prevents DevExpress from automatically modifying the ItemsSource collection.
    /// This stops it from trying to insert Clip objects into the TreeNodeBase collection.
    /// All data modifications are handled manually in TreeView_DropRecord.
    /// </summary>
    private void TreeView_CompleteRecordDragDrop(object sender, CompleteRecordDragDropEventArgs e) => e.Handled = true;
}
