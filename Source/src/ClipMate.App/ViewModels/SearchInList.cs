using System.Collections.ObjectModel;
using ClipMate.App.Models;
using ClipMate.App.Models.TreeNodes;
using ClipMate.App.Services;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace ClipMate.App.ViewModels;

/// <summary>
/// What the search dialog's "Search in" offers: "All Databases", then every Database, local and remote, each followed
/// by its collections and folders as the collection tree lays them out.
/// </summary>
internal sealed class SearchInList(IDatabases databases, ICollectionTreeBuilder collectionTreeBuilder, ILogger logger)
{
    private CancellationTokenSource? _loading;

    /// <summary>
    /// Gets the entries, in tree order. Filled by <see cref="LoadAsync" />.
    /// </summary>
    public ObservableCollection<SearchInOption> Options { get; } = [];

    /// <summary>
    /// Raised with the collections and folders of one Database, after they were added to <see cref="Options" />.
    /// </summary>
    public event Action<IReadOnlyList<SearchInOption>>? CollectionsAdded;

    /// <summary>
    /// The entry that searches <paramref name="target" />, or <c>null</c> while its collection hasn't arrived yet.
    /// </summary>
    public SearchInOption? Find(SearchTarget? target) => Options.FirstOrDefault(p => p.Searches(target));

    /// <summary>
    /// Fills <see cref="Options" />: every Database at once, then each one's collections and folders as its tree
    /// arrives, so a slow Peer holds up only its own. A Database that can't be read is grayed out. A load still running
    /// from the last time is dropped.
    /// </summary>
    public async Task LoadAsync()
    {
        if (_loading != null)
            await _loading.CancelAsync();

        using var loading = new CancellationTokenSource();
        _loading = loading;
        try
        {
            Options.Clear();
            var allDatabases = SearchTarget.AllDatabases;
            Options.Add(new SearchInOption(allDatabases, allDatabases.Name, 0, true) { Path = allDatabases.Name, Icon = AllDatabasesSearchTarget.Icon });

            var all = databases.All;
            foreach (var item in all)
            {
                var target = SearchTarget.For(item);
                Options.Add(new SearchInOption(target, target.Name, 0, item.IsAvailable) { Path = target.Name, Icon = DatabaseIcon(item, item.IsAvailable) });
            }

            await Task.WhenAll(all.Where(p => p.IsAvailable).Select(p => AddCollectionsAsync(p, loading.Token)));
        }
        finally
        {
            if (ReferenceEquals(_loading, loading))
                _loading = null;
        }
    }

    /// <summary>
    /// Adds a Database's collections and folders under its entry, or grays it out if its tree can't be read.
    /// </summary>
    private async Task AddCollectionsAsync(IDatabase database, CancellationToken cancellationToken)
    {
        IReadOnlyList<TreeNodeBase> nodes;
        try
        {
            // Smart and SQL-based collections, the Trashcan and Search Results have no clips of their own to search within
            nodes = await collectionTreeBuilder.BuildChildrenAsync(database, new DatabaseTreeNode(database.Name, database.Key),
                TreeNodeType.VirtualCollection | TreeNodeType.SpecialCollection, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            // An offline Peer or a missing file: the Database is still listed, grayed out
            logger.LogInformation("Search in: couldn't read the collections of {DatabaseKey}: {Reason}", database.Key, ex.Message);
            var index = IndexOfDatabase(database);
            if (index >= 0 && !cancellationToken.IsCancellationRequested)
                Options[index] = Options[index] with { IsAvailable = false, Icon = DatabaseIcon(database, false) };

            return;
        }

        var position = IndexOfDatabase(database);
        if (cancellationToken.IsCancellationRequested || position < 0)
            return;

        var added = EntriesFor(Options[position], nodes).ToList();
        foreach (var item in added)
            Options.Insert(++position, item);

        CollectionsAdded?.Invoke(added);
    }

    private int IndexOfDatabase(IDatabase database) =>
        Options.ToList().FindIndex(p => p.Target is DatabaseSearchTarget { Collection: null, Folder: null } target && target.DatabaseKey == database.Key);

    // The collection tree's icon for a Database: a local one that can't be read shows the error icon
    private static string DatabaseIcon(IDatabase database, bool isAvailable) =>
        database.Remote != null ? RemoteDatabaseTreeNode.DatabaseIcon : DatabaseTreeNode.IconFor(hasError: !isAvailable);

    /// <summary>
    /// The entries for <paramref name="nodes" /> and their folders, in tree order, under <paramref name="parent" />.
    /// </summary>
    /// <param name="parent">The Database, collection or folder the nodes are in.</param>
    /// <param name="nodes">The collection tree's nodes for it.</param>
    private static IEnumerable<SearchInOption> EntriesFor(SearchInOption parent, IEnumerable<TreeNodeBase> nodes)
    {
        var parentTarget = (DatabaseSearchTarget)parent.Target;
        foreach (var item in nodes)
        {
            var target = item switch
            {
                CollectionTreeNode { Collection.IsVirtual: false } node => parentTarget with { Collection = new NamedEntry(node.Collection.Id, node.Name), Folder = null },
                RemoteCollectionTreeNode { Collection.Kind: CollectionEntryKind.Collection or CollectionEntryKind.Folder } node =>
                    parentTarget with { Collection = new NamedEntry(node.Collection.Id, node.Name), Folder = null },
                FolderTreeNode node => parentTarget with { Folder = new NamedEntry(node.Folder.Id, node.Name) },
                RemoteFolderTreeNode node => parentTarget with { Folder = new NamedEntry(node.Folder.Id, node.Name) },
                var _ => null,
            };

            if (target == null)
                continue;

            var entry = SearchInOption.Under(parent, target, item.Name, item.Icon);
            yield return entry;

            foreach (var child in EntriesFor(entry, item.Children))
                yield return child;
        }
    }
}
