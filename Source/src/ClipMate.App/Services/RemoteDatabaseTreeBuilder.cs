using ClipMate.App.Models.TreeNodes;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services.Abstractions;

namespace ClipMate.App.Services;

/// <summary>
/// Builds the collection and folder nodes under a Remote Database node.
/// </summary>
public interface IRemoteDatabaseTreeBuilder
{
    /// <summary>
    /// Fetches the Remote Database's tree from its Peer and builds the child nodes of <paramref name="databaseNode" />
    /// (without adding them).
    /// </summary>
    /// <exception cref="RemoteDatabaseUnavailableException">The Peer cannot be reached or no longer shares the Database.</exception>
    Task<IReadOnlyList<TreeNodeBase>> BuildChildrenAsync(RemoteDatabaseTreeNode databaseNode, CancellationToken cancellationToken = default);
}

/// <summary>
/// Lays out a Remote Database like a local one: collections with their folders, then a "Virtual" container with the
/// Smart and SQL-based collections, ordered by this Device's collection sort preference.
/// </summary>
public sealed class RemoteDatabaseTreeBuilder : IRemoteDatabaseTreeBuilder
{
    private readonly IConfigurationService _configurationService;
    private readonly IRemoteDatabaseService _remoteDatabases;

    public RemoteDatabaseTreeBuilder(IRemoteDatabaseService remoteDatabases, IConfigurationService configurationService)
    {
        _remoteDatabases = remoteDatabases ?? throw new ArgumentNullException(nameof(remoteDatabases));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TreeNodeBase>> BuildChildrenAsync(RemoteDatabaseTreeNode databaseNode, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(databaseNode);
        var collections = await _remoteDatabases.GetCollectionTreeAsync(databaseNode.Address, cancellationToken);
        return BuildChildren(databaseNode, collections, _configurationService.Configuration.Preferences.SortCollectionsAlphabetically);
    }

    /// <summary>
    /// Builds the child nodes of <paramref name="databaseNode" /> from the flat list a Peer serves.
    /// </summary>
    public static IReadOnlyList<TreeNodeBase> BuildChildren(TreeNodeBase databaseNode, IReadOnlyList<SharedCollection> collections,
        bool sortAlphabetically)
    {
        ArgumentNullException.ThrowIfNull(databaseNode);
        ArgumentNullException.ThrowIfNull(collections);

        var ids = collections.Select(p => p.Id).ToHashSet();
        var folders = collections.Where(p => p.Kind == SharedCollectionKind.Folder && p.ParentId != null && ids.Contains(p.ParentId.Value))
            .ToLookup(p => p.ParentId!.Value);
        var placedFolders = folders.SelectMany(p => p).Select(p => p.Id).ToHashSet();

        // A folder whose parent is missing is shown as a collection rather than lost.
        var regular = collections.Where(p => p.Kind == SharedCollectionKind.Collection
                                             || p.Kind == SharedCollectionKind.Folder && !placedFolders.Contains(p.Id));
        var smart = collections.Where(p => p.Kind is SharedCollectionKind.Smart or SharedCollectionKind.SqlBased).ToList();

        var result = new List<TreeNodeBase>();
        var visited = new HashSet<Guid>();
        foreach (var item in Order(regular, sortAlphabetically))
        {
            var node = new RemoteCollectionTreeNode(item) { Parent = databaseNode, IsUnavailable = databaseNode.IsUnavailable };
            visited.Add(item.Id);
            AddFolders(node, item.Id, folders, visited);
            result.Add(node);
        }

        if (smart.Count > 0)
        {
            var container = new VirtualCollectionsContainerNode { Parent = databaseNode, IsUnavailable = databaseNode.IsUnavailable };
            foreach (var item in Order(smart, sortAlphabetically))
                container.Children.Add(new RemoteCollectionTreeNode(item) { Parent = container, IsUnavailable = databaseNode.IsUnavailable });

            result.Add(container);
        }

        return result;
    }

    private static void AddFolders(TreeNodeBase parent, Guid parentId, ILookup<Guid, SharedCollection> folders, HashSet<Guid> visited)
    {
        foreach (var item in folders[parentId].OrderBy(p => p.SortKey))
        {
            // A Peer's data could contain a cycle; show each folder once.
            if (!visited.Add(item.Id))
                continue;

            var node = new RemoteFolderTreeNode(item) { Parent = parent, IsUnavailable = parent.IsUnavailable };
            AddFolders(node, item.Id, folders, visited);
            parent.Children.Add(node);
        }
    }

    private static IEnumerable<SharedCollection> Order(IEnumerable<SharedCollection> collections, bool sortAlphabetically) =>
        sortAlphabetically
            ? collections.OrderBy(p => p.Title, StringComparer.CurrentCultureIgnoreCase)
            : collections.OrderBy(p => p.SortKey);
}
