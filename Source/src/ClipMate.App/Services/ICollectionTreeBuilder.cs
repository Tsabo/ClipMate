using ClipMate.App.Models.TreeNodes;
using ClipMate.Core.Services.Abstractions;

namespace ClipMate.App.Services;

/// <summary>
/// Builds the collection tree's nodes, the same way for every Database.
/// </summary>
public interface ICollectionTreeBuilder
{
    /// <summary>
    /// Builds a node for each local Database, in configured order, with its collections and folders (those of a
    /// Database whose file is missing are left out, and it is marked with an error).
    /// </summary>
    /// <param name="excludeNodes">
    /// Kinds of node to leave out: <see cref="TreeNodeType.VirtualCollection" /> leaves out the Smart and SQL-based
    /// collections, <see cref="TreeNodeType.SpecialCollection" /> the Trashcan and Search Results.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IEnumerable<TreeNodeBase>> BuildTreeAsync(TreeNodeType excludeNodes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the child nodes of <paramref name="databaseNode" /> (without adding them) from
    /// <paramref name="database" />'s collections and folders: collections with their folders, then a "Virtual"
    /// container with the Smart and SQL-based collections, ordered by the collection sort preference; then, for a local
    /// Database, the Trashcan and Search Results.
    /// </summary>
    /// <exception cref="DatabaseUnavailableException">The Database cannot be read.</exception>
    Task<IReadOnlyList<TreeNodeBase>> BuildChildrenAsync(IDatabase database, TreeNodeBase databaseNode, TreeNodeType excludeNodes = TreeNodeType.None,
        CancellationToken cancellationToken = default);
}
