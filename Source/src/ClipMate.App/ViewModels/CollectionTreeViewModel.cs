using System.Collections.ObjectModel;
using ClipMate.App.Models.TreeNodes;
using ClipMate.App.Services;
using ClipMate.App.Views.Dialogs;
using ClipMate.Core.Events;
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
public partial class CollectionTreeViewModel : ObservableObject, IRecipient<SearchExecutedEvent>
{
    private readonly IClipService _clipService;
    private readonly ICollectionService _collectionService;
    private readonly ICollectionTreeBuilder _collectionTreeBuilder;
    private readonly IConfigurationService _configurationService;
    private readonly IFolderService _folderService;
    private readonly ILogger<CollectionTreeViewModel> _logger;
    private readonly IMessenger _messenger;
    private readonly SearchResultsCache _searchResultsCache;

    [ObservableProperty]
    private TreeNodeBase? _selectedNode;

    public CollectionTreeViewModel(ICollectionService collectionService,
        IFolderService folderService,
        IClipService clipService,
        IConfigurationService configurationService,
        IMessenger messenger,
        ICollectionTreeBuilder collectionTreeBuilder,
        ILogger<CollectionTreeViewModel> logger,
        SearchResultsCache searchResultsCache)
    {
        _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
        _folderService = folderService ?? throw new ArgumentNullException(nameof(folderService));
        _clipService = clipService ?? throw new ArgumentNullException(nameof(clipService));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _collectionTreeBuilder = collectionTreeBuilder ?? throw new ArgumentNullException(nameof(collectionTreeBuilder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _searchResultsCache = searchResultsCache ?? throw new ArgumentNullException(nameof(searchResultsCache));

        // Register to receive SearchExecutedEvent
        _messenger.Register(this);
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


    partial void OnSelectedNodeChanged(TreeNodeBase? value)
    {
        _logger.LogInformation("Selection changed: NodeType={NodeType}", value?.GetType().Name ?? "null");

        // Get the database key by traversing up to the database node
        var databaseKey = GetDatabaseKeyForNode(value);
        if (string.IsNullOrEmpty(databaseKey))
        {
            _logger.LogWarning("Could not determine database key for selected node");
            return;
        }

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
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        RootNodes.Clear();
        var treeNodes = await _collectionTreeBuilder.BuildTreeAsync(
            TreeNodeType.None, cancellationToken);

        foreach (var item in treeNodes)
            RootNodes.Add(item);
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
                ShowDatabaseProperties(databaseNode);
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
    private void ShowDatabaseProperties(DatabaseTreeNode databaseNode)
    {
        // Get the database configuration using the database key
        var databaseKey = databaseNode.DatabasePath;
        var config = _configurationService.Configuration;

        if (!config.Databases.TryGetValue(databaseKey, out var databaseConfig))
        {
            _logger.LogWarning("Database configuration not found for key: {DatabaseKey}", databaseKey);
            return;
        }

        // Show the database edit dialog
        var dialog = new DatabaseEditDialog(databaseConfig)
        {
            Owner = Application.Current.GetDialogOwner(),
        };

        if (dialog.ShowDialog() != true || dialog.DatabaseConfig == null)
            return;

        // Update the configuration
        config.Databases[databaseKey] = dialog.DatabaseConfig;

        _logger.LogInformation("Updated database configuration: {DatabaseName}", dialog.DatabaseConfig.Name);

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
}
