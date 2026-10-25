using System.Collections.ObjectModel;
using ClipMate.App.Models;
using ClipMate.App.Models.TreeNodes;
using ClipMate.App.Services;
using ClipMate.App.Views.Dialogs;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Application = System.Windows.Application;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the collection/folder tree view.
/// Supports hierarchical structure: Database -> Collections -> Folders, plus Virtual Collections.
/// Sends CollectionNodeSelectedEvent via messenger when selection changes.
/// </summary>
public partial class CollectionTreeViewModel : ObservableObject, IRecipient<SearchExecutedEvent>, IRecipient<RefreshCollectionTreeRequestedEvent>,
    ISearchTargetSource, IDisposable
{
    private readonly IClipService _clipService;
    private readonly ICollectionService _collectionService;
    private readonly ICollectionTreeBuilder _collectionTreeBuilder;
    private readonly IConfigurationService _configurationService;
    private readonly IFolderService _folderService;
    private readonly ILogger<CollectionTreeViewModel> _logger;
    private readonly IMessenger _messenger;
    private readonly IPeerDialogService _peerDialogs;
    private readonly IDatabaseIdService _databaseIdService;
    private readonly Dictionary<RemoteDatabaseAddress, RemoteDatabaseTreeNode> _remoteDatabaseNodes = [];
    private readonly IRemoteDatabaseService _remoteDatabaseService;
    private readonly IRemoteDatabaseTreeBuilder _remoteDatabaseTreeBuilder;
    private readonly RemoteSearchResultsCache _remoteSearchResultsCache;
    private readonly SearchResultsCache _searchResultsCache;
    private readonly ISharedDatabaseService _sharedDatabaseService;
    private int _loadVersion;

    public CollectionTreeViewModel(ICollectionService collectionService,
        IFolderService folderService,
        IClipService clipService,
        IConfigurationService configurationService,
        IMessenger messenger,
        ICollectionTreeBuilder collectionTreeBuilder,
        ILogger<CollectionTreeViewModel> logger,
        SearchResultsCache searchResultsCache,
        IDatabaseIdService databaseIdService,
        IRemoteDatabaseService remoteDatabaseService,
        IRemoteDatabaseTreeBuilder remoteDatabaseTreeBuilder,
        ISharedDatabaseService sharedDatabaseService,
        IPeerDialogService peerDialogs,
        RemoteSearchResultsCache? remoteSearchResultsCache = null)
    {
        _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
        _folderService = folderService ?? throw new ArgumentNullException(nameof(folderService));
        _clipService = clipService ?? throw new ArgumentNullException(nameof(clipService));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _collectionTreeBuilder = collectionTreeBuilder ?? throw new ArgumentNullException(nameof(collectionTreeBuilder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _searchResultsCache = searchResultsCache ?? throw new ArgumentNullException(nameof(searchResultsCache));
        _databaseIdService = databaseIdService ?? throw new ArgumentNullException(nameof(databaseIdService));
        _remoteDatabaseService = remoteDatabaseService ?? throw new ArgumentNullException(nameof(remoteDatabaseService));
        _remoteDatabaseTreeBuilder = remoteDatabaseTreeBuilder ?? throw new ArgumentNullException(nameof(remoteDatabaseTreeBuilder));
        _sharedDatabaseService = sharedDatabaseService ?? throw new ArgumentNullException(nameof(sharedDatabaseService));
        _peerDialogs = peerDialogs ?? throw new ArgumentNullException(nameof(peerDialogs));
        _remoteSearchResultsCache = remoteSearchResultsCache ?? new RemoteSearchResultsCache();

        // Register to receive SearchExecutedEvent and RefreshCollectionTreeRequestedEvent
        _messenger.Register<SearchExecutedEvent>(this);
        _messenger.Register<RefreshCollectionTreeRequestedEvent>(this);

        // Remote Databases come and go with sharing, Peer status and Pairing, independently of the local tree
        _remoteDatabaseService.RemoteDatabasesChanged += OnRemoteDatabasesChanged;
    }

    /// <summary>
    /// Gets or sets the currently selected node in the collection tree.
    /// </summary>
    public virtual TreeNodeBase? SelectedNode
    {
        get;
        set
        {
            if (!SetProperty(ref field, value))
                return;

            RenameRemoteDatabaseCommand.NotifyCanExecuteChanged();
            OnSelectedNodeChanged(value);
        }
    }

    /// <summary>
    /// Root nodes of the tree (typically Database nodes).
    /// </summary>
    public ObservableCollection<TreeNodeBase> RootNodes { get; } = [];

    /// <summary>
    /// Receives SearchExecutedEvent to update search results in the virtual node.
    /// </summary>
    public void Receive(SearchExecutedEvent message)
    {
        _logger.LogInformation("Received SearchExecutedEvent: DatabaseKey={DatabaseKey}, Query={Query}, Count={Count}",
            message.DatabaseKey, message.Query, message.ClipIds.Count);

        // A Remote Search: its results are cached on this Device and shown under the Remote Database
        if (RemoteDatabaseKey.TryParse(message.DatabaseKey, out var remoteAddress))
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
                ShowRemoteSearchResults(remoteAddress);
            else
                dispatcher.InvokeAsync(() => ShowRemoteSearchResults(remoteAddress));

            return;
        }

        // Find the database node for this search
        var databaseNode = RootNodes.OfType<DatabaseTreeNode>()
            .FirstOrDefault(p => p.DatabasePath == message.DatabaseKey);

        if (databaseNode == null)
        {
            _logger.LogWarning("Database node not found for key: {DatabaseKey}", message.DatabaseKey);
            return;
        }

        // Get the search result from cache
        var searchResult = _searchResultsCache.GetResults(message.DatabaseKey);
        if (searchResult == null)
        {
            _logger.LogWarning("Search result not found in cache for database: {DatabaseKey}", message.DatabaseKey);
            return;
        }

        // UI updates must be on UI thread
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            // Find existing SearchResultsVirtualCollectionNode
            var existingNode = FindSearchResultsNode(databaseNode);

            if (existingNode != null)
            {
                // Update existing node with new search result
                _logger.LogInformation("Updating existing search results node");
                existingNode.SearchResult = searchResult;

                // Ensure the database node is expanded so the SearchResults node is visible
                if (!databaseNode.IsExpanded)
                {
                    _logger.LogInformation("Expanding database node to show search results");
                    databaseNode.IsExpanded = true;
                }

                // If the node is already selected, manually trigger the selection changed logic
                // to refresh the ClipList. Otherwise, just set SelectedNode.
                if (SelectedNode == existingNode)
                {
                    _logger.LogInformation("Search results node already selected, manually triggering refresh");
                    _messenger.Send(new SearchResultsSelectedEvent(message.DatabaseKey, searchResult.Query, searchResult.ClipIds));
                }
                else
                {
                    // Automatically select the SearchResults node to display results
                    _logger.LogInformation("Selecting search results node to display results");
                    SelectedNode = existingNode;
                }
            }
            else
                _logger.LogWarning("SearchResultsVirtualCollectionNode not found for database: {DatabaseKey}", message.DatabaseKey);
        });
    }

    /// <summary>
    /// Rebuilds the tree, e.g. after databases are added, removed or activated. Keeps the current selection when it still exists.
    /// </summary>
    public void Receive(RefreshCollectionTreeRequestedEvent message)
    {
        Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                var selectedDatabaseKey = GetDatabaseKeyForNode(SelectedNode);
                var selectedId = SelectedNode switch
                {
                    CollectionTreeNode collectionNode => collectionNode.Collection.Id,
                    VirtualCollectionTreeNode virtualNode => virtualNode.VirtualCollection.Id,
                    var _ => (Guid?)null,
                };

                if (!await LoadAsync())
                    return; // A newer refresh superseded this one

                // Collection GUIDs can repeat across databases, so look only within the previously selected database
                var databaseNode = RootNodes.OfType<DatabaseTreeNode>().FirstOrDefault(p => p.DatabasePath == selectedDatabaseKey);
                if (databaseNode is not null && selectedId is { } id && FindNodeByIdRecursive(databaseNode, id) is { } node)
                    SelectedNode = node;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh collection tree");
            }
        });
    }

    protected void OnSelectedNodeChanged(TreeNodeBase? value)
    {
        _logger.LogInformation("Selection changed: NodeType={NodeType}", value?.GetType().Name ?? "null");

        // A Remote Database is read-only: selecting it must never make it the active collection (the capture target)
        if (value is RemoteSearchResultsTreeNode remoteSearchNode)
        {
            if (remoteSearchNode.Result is { } result)
                _messenger.Send(new RemoteSearchResultsSelectedEvent(remoteSearchNode.Address, result));
            else
                _messenger.Send(new ShowSearchWindowEvent());

            return;
        }

        if (FindRemoteDatabaseNode(value) is { } remoteDatabaseNode)
        {
            var (collectionId, folderId) = GetRemoteSelection(value!);
            _messenger.Send(new RemoteDatabaseNodeSelectedEvent(remoteDatabaseNode.Address, collectionId, folderId));
            if (value is RemoteCollectionTreeNode or RemoteFolderTreeNode)
                _messenger.Send(new CurrentCollectionChangedEvent(ClipDescription.Collection(value.Name, remoteDatabaseNode.Database.PeerName)));

            return;
        }

        // Get the database key by traversing up to the database node
        var databaseKey = GetDatabaseKeyForNode(value);
        if (string.IsNullOrEmpty(databaseKey))
        {
            _logger.LogWarning("Could not determine database key for selected node");
            return;
        }

        if (value is CollectionTreeNode or FolderTreeNode or TrashcanVirtualCollectionNode or VirtualCollectionTreeNode or SearchResultsVirtualCollectionNode)
            _messenger.Send(new CurrentCollectionChangedEvent(ClipDescription.Collection(value.Name)));

        // Send messenger event with collection/folder IDs based on node type
        switch (value)
        {
            case CollectionTreeNode collectionNode:
                _logger.LogInformation("Sending CollectionNodeSelectedEvent: DatabaseKey={DatabaseKey}, CollectionId={CollectionId}, FolderId=null",
                    databaseKey, collectionNode.Collection.Id);

                _messenger.Send(new CollectionNodeSelectedEvent(collectionNode.Collection.Id, null, databaseKey));

                break;

            case FolderTreeNode folderNode:
                _logger.LogInformation("Sending CollectionNodeSelectedEvent: DatabaseKey={DatabaseKey}, CollectionId={CollectionId}, FolderId={FolderId}",
                    databaseKey, folderNode.Folder.CollectionId, folderNode.Folder.Id);

                _messenger.Send(new CollectionNodeSelectedEvent(folderNode.Folder.CollectionId, folderNode.Folder.Id, databaseKey));

                break;

            case TrashcanVirtualCollectionNode trashcanNode:
                _logger.LogInformation("Sending CollectionNodeSelectedEvent for Trashcan: DatabaseKey={DatabaseKey}", databaseKey);

                _messenger.Send(new CollectionNodeSelectedEvent(trashcanNode.VirtualId, null, databaseKey, true));

                break;

            case SearchResultsVirtualCollectionNode searchNode:
                if (searchNode.SearchResult is { ClipIds.Count: > 0 })
                {
                    _logger.LogInformation("Sending SearchResultsSelectedEvent: DatabaseKey={DatabaseKey}, Query={Query}, Count={Count}",
                        databaseKey, searchNode.SearchResult.Query, searchNode.SearchResult.Count);

                    _messenger.Send(new SearchResultsSelectedEvent(databaseKey, searchNode.SearchResult.Query, searchNode.SearchResult.ClipIds));
                }
                else
                {
                    // No search results - show search window
                    _logger.LogInformation("No search results - triggering search window display");
                    _messenger.Send(new ShowSearchWindowEvent());
                }

                break;

            case VirtualCollectionTreeNode virtualNode:
                _logger.LogInformation("Sending CollectionNodeSelectedEvent: DatabaseKey={DatabaseKey}, CollectionId={CollectionId}, FolderId=null",
                    databaseKey, virtualNode.VirtualCollection.Id);

                _messenger.Send(new CollectionNodeSelectedEvent(virtualNode.VirtualCollection.Id, null, databaseKey));

                break;

            // Database and VirtualCollectionsContainer nodes don't trigger selection changes
        }
    }

    /// <summary>
    /// Gets the database configuration key for a tree node by traversing up to the database node.
    /// </summary>
    private string? GetDatabaseKeyForNode(TreeNodeBase? node)
    {
        if (node == null)
            return null;

        // Traverse up the tree to find the DatabaseTreeNode
        var current = node;
        while (current != null)
        {
            if (current is DatabaseTreeNode dbNode)
                return dbNode.DatabasePath;

            current = current.Parent;
        }

        return null;
    }

    /// <summary>
    /// Loads the complete tree hierarchy: Database -> Collections/Virtual Collections -> Folders.
    /// Creates a database node for each configured database.
    /// </summary>
    /// <returns>False if a newer load started while this one was building, in which case the result was discarded.</returns>
    public async Task<bool> LoadAsync(CancellationToken cancellationToken = default)
    {
        // Build before clearing, and discard stale results, so overlapping loads can't leave duplicate or outdated nodes
        var version = Interlocked.Increment(ref _loadVersion);
        var treeNodes = await _collectionTreeBuilder.BuildTreeAsync(
            TreeNodeType.None, cancellationToken);

        if (version != Volatile.Read(ref _loadVersion))
            return false;

        RootNodes.Clear();
        foreach (var item in treeNodes)
            RootNodes.Add(item);

        // Remote Databases follow the local ones; their trees load in the background so a slow Peer never delays the local tree
        foreach (var item in _remoteDatabaseNodes.Values)
            RootNodes.Add(item);

        _ = RefreshRemoteDatabasesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Brings the Remote Database nodes in line with <see cref="IRemoteDatabaseService" />: adds new ones, removes those
    /// that are no longer shared or whose Peer was unpaired, greys out those whose Peer is offline, and fetches the
    /// collection tree of each available one that has not been loaded yet. Must run on the UI thread.
    /// </summary>
    public async Task RefreshRemoteDatabasesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        var remoteDatabases = _remoteDatabaseService.GetRemoteDatabases();
        var current = remoteDatabases.Select(p => p.Address).ToHashSet();

        foreach (var item in _remoteDatabaseNodes.Values.Where(p => !current.Contains(p.Address)).ToList())
        {
            if (FindRemoteDatabaseNode(SelectedNode) == item)
                SelectedNode = null;

            RootNodes.Remove(item);
            _remoteDatabaseNodes.Remove(item.Address);

            // Unshared, or its Peer was unpaired: its search results go too
            _remoteSearchResultsCache.ClearResults(item.Address);
        }

        var toLoad = new List<RemoteDatabaseTreeNode>();
        foreach (var item in remoteDatabases)
        {
            if (_remoteDatabaseNodes.TryGetValue(item.Address, out var node))
            {
                // The Peer's tree may have changed while it was away
                if (item.IsAvailable && !node.Database.IsAvailable)
                    node.ChildrenLoaded = false;

                node.Update(item, now);
            }
            else
            {
                node = new RemoteDatabaseTreeNode(item, now);
                _remoteDatabaseNodes.Add(item.Address, node);
                RootNodes.Add(node);
            }

            if (item.IsAvailable && !node.ChildrenLoaded)
                toLoad.Add(node);
        }

        await Task.WhenAll(toLoad.Select(p => LoadRemoteDatabaseChildrenAsync(p, cancellationToken)));
    }

    private async Task LoadRemoteDatabaseChildrenAsync(RemoteDatabaseTreeNode node, CancellationToken cancellationToken)
    {
        node.ChildrenLoaded = true;
        try
        {
            var children = await _remoteDatabaseTreeBuilder.BuildChildrenAsync(node, cancellationToken);
            if (!_remoteDatabaseNodes.ContainsKey(node.Address))
                return; // Removed meanwhile

            node.Children.Clear();
            foreach (var item in children)
                node.Children.Add(item);

            node.Children.Add(new RemoteSearchResultsTreeNode(node.Address, _remoteSearchResultsCache.GetResults(node.Address))
            {
                Parent = node,
                IsUnavailable = node.IsUnavailable,
            });
        }
        catch (Exception ex) when (ex is RemoteDatabaseUnavailableException or OperationCanceledException)
        {
            node.ChildrenLoaded = false;
            _logger.LogInformation("Could not load Remote Database {RemoteDatabase}: {Reason}", node.Address, ex.Message);
        }
        catch (Exception ex)
        {
            node.ChildrenLoaded = false;
            _logger.LogWarning(ex, "Failed to load Remote Database {RemoteDatabase}", node.Address);
        }
    }

    /// <summary>
    /// Shows the cached Remote Search results of a Remote Database in its Search Results node and selects it, so the
    /// clip list shows them. Must run on the UI thread.
    /// </summary>
    private void ShowRemoteSearchResults(RemoteDatabaseAddress address)
    {
        var result = _remoteSearchResultsCache.GetResults(address);
        if (result == null || !_remoteDatabaseNodes.TryGetValue(address, out var databaseNode))
        {
            _logger.LogWarning("No Search Results node or results for Remote Database {RemoteDatabase}", address);
            return;
        }

        var searchNode = databaseNode.Children.OfType<RemoteSearchResultsTreeNode>().FirstOrDefault();
        if (searchNode == null)
            return;

        searchNode.Result = result;
        databaseNode.IsExpanded = true;

        if (SelectedNode == searchNode)
            _messenger.Send(new RemoteSearchResultsSelectedEvent(address, result));
        else
            SelectedNode = searchNode;
    }

    /// <inheritdoc />
    public SearchTarget? GetSearchTarget()
    {
        var node = SelectedNode;
        if (node == null)
            return null;

        if (FindRemoteDatabaseNode(node) is { } remoteDatabaseNode)
        {
            var target = new SearchTarget(RemoteDatabaseKey.From(remoteDatabaseNode.Address), remoteDatabaseNode.Database.DisplayName);
            return node switch
            {
                // Smart and SQL-based collections have no clips of their own to search within
                RemoteCollectionTreeNode { Collection.Kind: SharedCollectionKind.Collection or SharedCollectionKind.Folder } collection =>
                    target with { CollectionId = collection.Collection.Id, CollectionName = collection.Name },
                RemoteFolderTreeNode folder => folder.Parent is { } folderParent && FindAncestor<RemoteCollectionTreeNode>(folderParent) is { } parent
                    ? target with { CollectionId = parent.Collection.Id, CollectionName = parent.Name, FolderId = folder.Folder.Id, FolderName = folder.Name }
                    : target with { FolderId = folder.Folder.Id, FolderName = folder.Name },
                var _ => target,
            };
        }

        if (FindAncestor<DatabaseTreeNode>(node) is not { } databaseNode)
            return null;

        var local = new SearchTarget(databaseNode.DatabasePath, databaseNode.Name);
        return node switch
        {
            CollectionTreeNode { Collection.IsVirtual: false } collection =>
                local with { CollectionId = collection.Collection.Id, CollectionName = collection.Name },
            FolderTreeNode folder => local with
            {
                CollectionId = folder.Folder.CollectionId,
                CollectionName = FindAncestor<CollectionTreeNode>(folder)?.Name,
                FolderId = folder.Folder.Id,
                FolderName = folder.Name,
            },
            var _ => local,
        };
    }

    /// <summary>
    /// Returns <paramref name="node" /> or its nearest ancestor of type <typeparamref name="T" />.
    /// </summary>
    private static T? FindAncestor<T>(TreeNodeBase node) where T : TreeNodeBase
    {
        for (var current = node; current != null; current = current.Parent)
        {
            if (current is T match)
                return match;
        }

        return null;
    }

    private void OnRemoteDatabasesChanged(object? sender, EventArgs e)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
            return;

        dispatcher.InvokeAsync(async () =>
        {
            try
            {
                await RefreshRemoteDatabasesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh Remote Databases in the collection tree");
            }
        });
    }

    /// <summary>
    /// Returns the Remote Database node that <paramref name="node" /> is in, or null for a local node.
    /// </summary>
    private static RemoteDatabaseTreeNode? FindRemoteDatabaseNode(TreeNodeBase? node)
    {
        for (var current = node; current != null; current = current.Parent)
        {
            if (current is RemoteDatabaseTreeNode remoteDatabaseNode)
                return remoteDatabaseNode;
        }

        return null;
    }

    /// <summary>
    /// Sets or clears the Remote Database Alias of the selected Remote Database node. Only the local label changes;
    /// the Peer's Database name is untouched. Works while the Peer is offline.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRenameRemoteDatabase))]
    private async Task RenameRemoteDatabaseAsync()
    {
        if (SelectedNode is not RemoteDatabaseTreeNode node)
            return;

        var alias = _peerDialogs.AskRemoteDatabaseAlias(node.Database);
        if (alias == null)
            return;

        try
        {
            await _remoteDatabaseService.SetAliasAsync(node.Address, alias);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rename Remote Database {RemoteDatabase}", node.Address);
        }
    }

    private bool CanRenameRemoteDatabase() => SelectedNode is RemoteDatabaseTreeNode;

    private static (Guid? CollectionId, Guid? FolderId) GetRemoteSelection(TreeNodeBase node)
    {
        if (node is RemoteCollectionTreeNode collectionNode)
            return (collectionNode.Collection.Id, null);

        if (node is not RemoteFolderTreeNode folderNode)
            return (null, null);

        for (var current = node.Parent; current != null; current = current.Parent)
        {
            if (current is RemoteCollectionTreeNode parentCollection)
                return (parentCollection.Collection.Id, folderNode.Folder.Id);
        }

        return (null, folderNode.Folder.Id);
    }

    /// <summary>
    /// Creates a new collection.
    /// </summary>
    [RelayCommand]
    private async Task CreateCollectionAsync((string name, string? description) parameters)
    {
        await _collectionService.CreateAsync(parameters.name, parameters.description);
        await LoadAsync();
    }

    /// <summary>
    /// Creates a new folder.
    /// </summary>
    [RelayCommand]
    private async Task CreateFolderAsync((string name, Guid collectionId, Guid? parentFolderId) parameters)
    {
        await _folderService.CreateAsync(parameters.name, parameters.collectionId, parameters.parentFolderId);
        await LoadAsync();
    }

    /// <summary>
    /// Deletes a collection.
    /// </summary>
    [RelayCommand]
    private async Task DeleteCollectionAsync(Guid collectionId)
    {
        await _collectionService.DeleteAsync(collectionId);
        await LoadAsync();
    }

    /// <summary>
    /// Deletes a folder.
    /// </summary>
    [RelayCommand]
    private async Task DeleteFolderAsync(Guid folderId)
    {
        await _folderService.DeleteAsync(folderId);
        await LoadAsync();
    }

    /// <summary>
    /// Shows properties dialog for the selected collection or folder.
    /// </summary>
    [RelayCommand]
    private async Task ShowPropertiesAsync()
    {
        if (SelectedNode == null)
            return;

        switch (SelectedNode)
        {
            case DatabaseTreeNode databaseNode:
                await ShowDatabasePropertiesAsync(databaseNode);
                break;

            case CollectionTreeNode collectionNode:
                await ShowCollectionPropertiesAsync(collectionNode.Collection.Id);

                break;

            case VirtualCollectionTreeNode virtualNode:
                await ShowCollectionPropertiesAsync(virtualNode.VirtualCollection.Id);

                break;

            // Folders don't have properties yet, but could be added in the future
            case FolderTreeNode folderNode:
                _logger.LogInformation("Folder properties not yet implemented for: {FolderName}", folderNode.Name);

                break;
        }
    }

    /// <summary>
    /// Shows the collection properties dialog.
    /// </summary>
    private async Task ShowCollectionPropertiesAsync(Guid collectionId)
    {
        var collection = await _collectionService.GetByIdAsync(collectionId);
        if (collection == null)
        {
            _logger.LogWarning("Collection not found: {CollectionId}", collectionId);

            return;
        }

        // Get the active database key
        var activeDatabaseKey = _collectionService.GetActiveDatabaseKey();

        var viewModel = new CollectionPropertiesViewModel(
            collection,
            _configurationService,
            null!,
            activeDatabaseKey);

        var window = new CollectionPropertiesDialog(viewModel, _configurationService)
        {
            Owner = Application.Current.GetDialogOwner(),
        };

        if (window.ShowDialog() == true)
        {
            // Sync SQL editor text to ViewModel before saving
            window.SyncSqlEditorToViewModel();
            viewModel.SaveToModel();

            // Save changes to database
            await _collectionService.UpdateAsync(collection);
            await LoadAsync(); // Reload tree to reflect changes
        }
    }

    /// <summary>
    /// Shows the database properties dialog for editing database configuration.
    /// </summary>
    private async Task ShowDatabasePropertiesAsync(DatabaseTreeNode databaseNode)
    {
        // Get the database configuration using the database key
        var databaseKey = databaseNode.DatabasePath;
        var config = _configurationService.Configuration;

        if (!config.Databases.TryGetValue(databaseKey, out var databaseConfig))
        {
            _logger.LogWarning("Database configuration not found for key: {DatabaseKey}", databaseKey);
            return;
        }

        var databaseId = await _databaseIdService.GetDatabaseIdAsync(databaseKey);

        var otherDatabases = config.Databases.Where(p => p.Key != databaseKey).Select(p => p.Value).ToList();
        var sharing = new DatabaseSharingContext(_sharedDatabaseService, _databaseIdService, otherDatabases);

        // Show the database edit dialog
        var dialog = new DatabaseEditDialog(databaseConfig, databaseId, sharing)
        {
            Owner = Application.Current.GetDialogOwner(),
        };

        if (dialog.ShowDialog() != true || dialog.DatabaseConfig == null)
            return;

        // Update the configuration
        config.Databases[databaseKey] = dialog.DatabaseConfig;

        _logger.LogInformation("Updated database configuration: {DatabaseName}", dialog.DatabaseConfig.Name);

        // Persist at once: sharing takes effect for Peers immediately and must survive a restart
        await _configurationService.SaveAsync();

        // Reload the tree to reflect the changes
        _ = LoadAsync();
    }

    /// <summary>
    /// Moves the selected collection up in the sort order (decreases SortKey).
    /// Keyboard shortcut: +
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private async Task MoveUpAsync()
    {
        if (SelectedNode is not CollectionTreeNode collectionNode)
            return;

        var databaseKey = GetDatabaseKeyForNode(collectionNode);
        if (string.IsNullOrEmpty(databaseKey))
        {
            _logger.LogWarning("Cannot move collection: database key not found");
            return;
        }

        var moved = await _collectionService.MoveCollectionUpAsync(collectionNode.Collection.Id, databaseKey);

        if (moved)
        {
            _logger.LogInformation("Moved collection {CollectionName} up", collectionNode.Collection.Name);
            await LoadAsync(); // Reload tree to reflect new sort order
        }
    }

    /// <summary>
    /// Determines if the selected collection can be moved up.
    /// </summary>
    private bool CanMoveUp()
    {
        if (SelectedNode is not CollectionTreeNode collectionNode)
            return false;

        // Cannot move virtual collections
        if (collectionNode.Collection.IsVirtual)
            return false;

        // Cannot manually reorder when alphabetic sorting is enabled (global preference)
        if (_configurationService.Configuration.Preferences.SortCollectionsAlphabetically)
            return false;

        // Check if there's a collection above this one (not at index 0)
        if (collectionNode.Parent is not DatabaseTreeNode parent)
            return false;

        var siblings = parent.Children.OfType<CollectionTreeNode>()
            .Where(p => !p.Collection.IsVirtual)
            .OrderBy(p => p.Collection.SortKey)
            .ToList();

        var currentIndex = siblings.FindIndex(p => p.Collection.Id == collectionNode.Collection.Id);
        return currentIndex > 0;
    }

    /// <summary>
    /// Moves the selected collection down in the sort order (increases SortKey).
    /// Keyboard shortcut: -
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private async Task MoveDownAsync()
    {
        if (SelectedNode is not CollectionTreeNode collectionNode)
            return;

        var databaseKey = GetDatabaseKeyForNode(collectionNode);
        if (string.IsNullOrEmpty(databaseKey))
        {
            _logger.LogWarning("Cannot move collection: database key not found");
            return;
        }

        var moved = await _collectionService.MoveCollectionDownAsync(collectionNode.Collection.Id, databaseKey);

        if (moved)
        {
            _logger.LogInformation("Moved collection {CollectionName} down", collectionNode.Collection.Name);
            await LoadAsync(); // Reload tree to reflect new sort order
        }
    }

    /// <summary>
    /// Determines if the selected collection can be moved down.
    /// </summary>
    private bool CanMoveDown()
    {
        if (SelectedNode is not CollectionTreeNode collectionNode)
            return false;

        // Cannot move virtual collections
        if (collectionNode.Collection.IsVirtual)
            return false;

        // Cannot manually reorder when alphabetic sorting is enabled (global preference)
        if (_configurationService.Configuration.Preferences.SortCollectionsAlphabetically)
            return false;

        // Check if there's a collection below this one
        if (collectionNode.Parent is not DatabaseTreeNode parent)
            return false;

        var siblings = parent.Children.OfType<CollectionTreeNode>()
            .Where(p => !p.Collection.IsVirtual)
            .OrderBy(p => p.Collection.SortKey)
            .ToList();

        var currentIndex = siblings.FindIndex(p => p.Collection.Id == collectionNode.Collection.Id);
        return currentIndex >= 0 && currentIndex < siblings.Count - 1;
    }

    /// <summary>
    /// Reorders collections after a drag-drop operation by updating SortKey values.
    /// </summary>
    /// <param name="droppedCollectionIds">IDs of collections being dropped.</param>
    /// <param name="targetCollectionId">ID of the target collection.</param>
    /// <param name="insertAfter">True to insert after target, false to insert before.</param>
    public async Task ReorderCollectionsAsync(List<Guid> droppedCollectionIds, Guid targetCollectionId, bool insertAfter)
    {
        if (droppedCollectionIds.Count == 0)
            return;

        // Get database key from the first dropped collection
        var firstDroppedNode = FindNodeById(droppedCollectionIds.First());
        if (firstDroppedNode == null)
        {
            _logger.LogWarning("Could not find dropped collection node");
            return;
        }

        var databaseKey = GetDatabaseKeyForNode(firstDroppedNode);
        if (string.IsNullOrEmpty(databaseKey))
        {
            _logger.LogWarning("Cannot reorder collections: database key not found");
            return;
        }

        await _collectionService.ReorderCollectionsAsync(droppedCollectionIds, targetCollectionId, insertAfter, databaseKey);

        _logger.LogInformation("Reordered {Count} collections", droppedCollectionIds.Count);
        await LoadAsync(); // Reload tree to reflect new order
    }

    /// <summary>
    /// Finds a tree node by collection ID.
    /// </summary>
    private TreeNodeBase? FindNodeById(Guid collectionId) => RootNodes.Select(p => FindNodeByIdRecursive(p, collectionId)).OfType<TreeNodeBase>().FirstOrDefault();

    /// <summary>
    /// Recursively searches for a node by collection ID.
    /// </summary>
    private TreeNodeBase? FindNodeByIdRecursive(TreeNodeBase node, Guid collectionId)
    {
        if (node is CollectionTreeNode collectionNode && collectionNode.Collection.Id == collectionId || node is VirtualCollectionTreeNode virtualNode && virtualNode.VirtualCollection.Id == collectionId)
            return node;

        return node.Children.Select(p => FindNodeByIdRecursive(p, collectionId)).OfType<TreeNodeBase>().FirstOrDefault();
    }

    /// <summary>
    /// Moves clips from one collection to another.
    /// Updates both CollectionId and DatabaseId if moving across databases.
    /// </summary>
    public async Task MoveClipsToCollectionAsync(List<Guid> clipIds, Guid targetCollectionId, Guid? targetDatabaseId)
    {
        // Find the target collection node to get the database key
        var targetNode = FindNodeById(targetCollectionId);
        if (targetNode == null)
        {
            _logger.LogError("Target collection not found: {CollectionId}", targetCollectionId);
            return;
        }

        // Find the database node by walking up the tree
        var databaseNode = GetDatabaseNode(targetNode);
        if (databaseNode == null)
        {
            _logger.LogError("Could not find database node for collection {CollectionId}", targetCollectionId);
            return;
        }

        // Use service to move clips to the collection
        await _clipService.MoveClipsToCollectionAsync(databaseNode.DatabasePath, clipIds, targetCollectionId);

        _logger.LogInformation("Moved {Count} clips to collection {CollectionId}", clipIds.Count, targetCollectionId);
    }

    /// <summary>
    /// Soft-deletes clips by setting Del=true (moves to Trashcan).
    /// </summary>
    public async Task SoftDeleteClipsAsync(List<Guid> clipIds, string databaseKey)
    {
        await _clipService.SoftDeleteClipsAsync(databaseKey, clipIds);

        _logger.LogInformation("Soft-deleted {Count} clips to Trashcan", clipIds.Count);
    }

    /// <summary>
    /// Restores clips from Trashcan by setting Del=false and moving to target collection.
    /// </summary>
    public async Task RestoreClipsAsync(List<Guid> clipIds, Guid targetCollectionId, string databaseKey)
    {
        await _clipService.RestoreClipsAsync(databaseKey, clipIds, targetCollectionId);

        _logger.LogInformation("Restored {Count} clips from Trashcan to collection {CollectionId}", clipIds.Count, targetCollectionId);
    }

    /// <summary>
    /// Walks up the tree to find the DatabaseTreeNode ancestor.
    /// </summary>
    private DatabaseTreeNode? GetDatabaseNode(TreeNodeBase node)
    {
        // Walk up the tree to find the database node
        foreach (var item in RootNodes)
        {
            if (item is DatabaseTreeNode dbNode && IsDescendantOf(node, dbNode))
                return dbNode;
        }

        return null;
    }

    /// <summary>
    /// Checks if a node is a descendant of a potential ancestor.
    /// </summary>
    private bool IsDescendantOf(TreeNodeBase node, TreeNodeBase potentialAncestor) => node == potentialAncestor || potentialAncestor.Children.Any(p => IsDescendantOf(node, p));

    /// <summary>
    /// Finds the SearchResultsVirtualCollectionNode within a database node's children.
    /// </summary>
    private SearchResultsVirtualCollectionNode? FindSearchResultsNode(DatabaseTreeNode databaseNode) => databaseNode.Children.OfType<SearchResultsVirtualCollectionNode>().FirstOrDefault();

    /// <summary>
    /// Expands all nodes in the tree.
    /// </summary>
    [RelayCommand]
    private void ExpandAll()
    {
        _logger.LogInformation("Expanding all nodes in collection tree");
        _messenger.Send(new ExpandAllNodesRequestedEvent());
    }

    /// <summary>
    /// Collapses all nodes in the tree.
    /// </summary>
    [RelayCommand]
    private void CollapseAll()
    {
        _logger.LogInformation("Collapsing all nodes in collection tree");
        _messenger.Send(new CollapseAllNodesRequestedEvent());
    }

    /// <summary>
    /// Shows all clips in the selected collection and all child folders recursively.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanShowAllClipsInChildren))]
    private void ShowAllClipsInChildren()
    {
        if (SelectedNode is not CollectionTreeNode collectionNode)
            return;

        var databaseKey = GetDatabaseKeyForNode(collectionNode);
        if (string.IsNullOrEmpty(databaseKey))
        {
            _logger.LogWarning("Cannot show all clips: database key not found");
            return;
        }

        _logger.LogInformation("Showing all clips in collection {CollectionId} and children", collectionNode.Collection.Id);
        _messenger.Send(new ShowAllClipsInChildrenRequestedEvent(collectionNode.Collection.Id, databaseKey));
    }

    /// <summary>
    /// Determines if "Show All Clips In All Children" can be executed.
    /// Only enabled for non-virtual collections.
    /// </summary>
    private bool CanShowAllClipsInChildren()
    {
        if (SelectedNode is not CollectionTreeNode collectionNode)
            return false;

        // Cannot use this on virtual collections or trashcan
        return !collectionNode.Collection.IsVirtual;
    }

    /// <summary>
    /// Disposes the view model and unregisters from messenger.
    /// </summary>
    public void Dispose()
    {
        _messenger.UnregisterAll(this);
        _remoteDatabaseService.RemoteDatabasesChanged -= OnRemoteDatabasesChanged;
    }
}
