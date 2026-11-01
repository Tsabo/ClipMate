using ClipMate.App.Models.TreeNodes;
using ClipMate.Core.Models;
using ClipMate.Core.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace ClipMate.App.Services;

/// <summary>
/// <see cref="ICollectionTreeBuilder" /> over <see cref="IDatabases" />. Every Database's collections and folders are
/// laid out by one rule from its flat <see cref="CollectionEntry" /> list. A local Database's nodes also carry its
/// <see cref="Collection" /> and <see cref="Folder" /> objects, which the tree's styles and its collection commands use.
/// </summary>
internal class CollectionTreeBuilder : ICollectionTreeBuilder
{
    private readonly ICollectionService _collectionService;
    private readonly IConfigurationService _configurationService;
    private readonly IDatabases _databases;
    private readonly ILogger<CollectionTreeBuilder> _logger;

    public CollectionTreeBuilder(IConfigurationService configurationService,
        IDatabases databases,
        ICollectionService collectionService,
        ILogger<CollectionTreeBuilder> logger)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _databases = databases ?? throw new ArgumentNullException(nameof(databases));
        _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TreeNodeBase>> BuildTreeAsync(TreeNodeType excludeNodes, CancellationToken cancellationToken = default)
    {
        var result = new List<TreeNodeBase>();
        foreach (var item in _databases.All.Where(p => p.Remote == null))
        {
            var databaseNode = new DatabaseTreeNode(item.Name, item.Key, !item.IsAvailable);
            if (item.IsAvailable)
            {
                try
                {
                    foreach (var child in await BuildChildrenAsync(item, databaseNode, excludeNodes, cancellationToken))
                        databaseNode.Children.Add(child);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Failed to load collections from database: {DatabaseName}", item.Name);
                }
            }

            // Expand the default database node by default
            if (item.Key == _configurationService.Configuration.DefaultDatabase)
                databaseNode.IsExpanded = true;

            result.Add(databaseNode);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TreeNodeBase>> BuildChildrenAsync(IDatabase database, TreeNodeBase databaseNode,
        TreeNodeType excludeNodes = TreeNodeType.None, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(databaseNode);

        var entries = await database.GetTreeAsync(cancellationToken);

        // A local Database's nodes carry the collections themselves, for the tree's styles and its collection commands
        var collections = database.Remote == null
            ? (await _collectionService.GetAllByDatabaseKeyAsync(database.Key, cancellationToken)).ToDictionary(p => p.Id)
            : [];

        var result = BuildChildren(databaseNode, entries, collections, _configurationService.Configuration.Preferences.SortCollectionsAlphabetically,
            !excludeNodes.HasFlag(TreeNodeType.VirtualCollection));

        // The Trashcan and Search Results of a local Database follow its collections
        if (database.Remote == null && !excludeNodes.HasFlag(TreeNodeType.SpecialCollection))
        {
            result.Add(new TrashcanVirtualCollectionNode(database.Key) { Parent = databaseNode });
            result.Add(new SearchResultsVirtualCollectionNode(database.Key) { Parent = databaseNode });
        }

        return result;
    }

    /// <summary>
    /// Lays out the flat list: collections with their folders (a folder whose parent is missing is shown as a
    /// collection rather than lost), then a "Virtual" container with the Smart and SQL-based collections.
    /// </summary>
    private static List<TreeNodeBase> BuildChildren(TreeNodeBase databaseNode, IReadOnlyList<CollectionEntry> entries,
        Dictionary<Guid, Collection> collections, bool sortAlphabetically, bool includeVirtual)
    {
        var ids = entries.Select(p => p.Id).ToHashSet();
        var folders = entries.Where(p => p.Kind == CollectionEntryKind.Folder && p.ParentId != null && ids.Contains(p.ParentId.Value))
            .ToLookup(p => p.ParentId!.Value);
        var placedFolders = folders.SelectMany(p => p).Select(p => p.Id).ToHashSet();

        var regular = entries.Where(p => p.Kind == CollectionEntryKind.Collection
                                         || p.Kind == CollectionEntryKind.Folder && !placedFolders.Contains(p.Id));
        var smart = entries.Where(p => p.Kind is CollectionEntryKind.Smart or CollectionEntryKind.SqlBased).ToList();

        var result = new List<TreeNodeBase>();
        var visited = new HashSet<Guid>();
        foreach (var item in Order(regular, sortAlphabetically))
        {
            var node = CreateCollectionNode(item, collections, databaseNode);
            visited.Add(item.Id);
            AddFolders(node, item.Id, folders, collections, visited);
            result.Add(node);
        }

        if (includeVirtual && smart.Count > 0)
        {
            var container = new VirtualCollectionsContainerNode { Parent = databaseNode, IsUnavailable = databaseNode.IsUnavailable };
            foreach (var item in Order(smart, sortAlphabetically))
                container.Children.Add(CreateCollectionNode(item, collections, container));

            result.Add(container);
        }

        return result;
    }

    private static void AddFolders(TreeNodeBase parent, Guid parentId, ILookup<Guid, CollectionEntry> folders, Dictionary<Guid, Collection> collections,
        HashSet<Guid> visited)
    {
        foreach (var item in folders[parentId].OrderBy(p => p.SortKey))
        {
            // Stored data could contain a cycle; show each folder once
            if (!visited.Add(item.Id))
                continue;

            TreeNodeBase node = collections.TryGetValue(item.Id, out var collection)
                ? new FolderTreeNode(Folder.FromCollection(collection))
                : new RemoteFolderTreeNode(item);

            node.Parent = parent;
            node.IsUnavailable = parent.IsUnavailable;
            AddFolders(node, item.Id, folders, collections, visited);
            parent.Children.Add(node);
        }
    }

    private static TreeNodeBase CreateCollectionNode(CollectionEntry entry, Dictionary<Guid, Collection> collections, TreeNodeBase parent)
    {
        TreeNodeBase node = collections.TryGetValue(entry.Id, out var collection)
            ? entry.Kind is CollectionEntryKind.Smart or CollectionEntryKind.SqlBased
                ? new VirtualCollectionTreeNode(collection)
                : new CollectionTreeNode(collection)
            : new RemoteCollectionTreeNode(entry);

        node.Parent = parent;
        node.IsUnavailable = parent.IsUnavailable;
        return node;
    }

    private static IEnumerable<CollectionEntry> Order(IEnumerable<CollectionEntry> entries, bool sortAlphabetically) =>
        sortAlphabetically
            ? entries.OrderBy(p => p.Title, StringComparer.CurrentCultureIgnoreCase)
            : entries.OrderBy(p => p.SortKey);
}
