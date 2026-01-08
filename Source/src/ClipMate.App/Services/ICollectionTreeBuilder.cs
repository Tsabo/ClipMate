using ClipMate.App.Models.TreeNodes;

namespace ClipMate.App.Services;

public interface ICollectionTreeBuilder
{
    Task<IEnumerable<TreeNodeBase>> BuildTreeAsync(TreeNodeType excludeNodes, CancellationToken cancellationToken = default);
}
