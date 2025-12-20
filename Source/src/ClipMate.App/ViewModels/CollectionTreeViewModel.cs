using System.Collections.ObjectModel;
using ClipMate.App.Services;
using ClipMate.App.Views;
using ClipMate.App.Views.Dialogs;
using ClipMate.Core.Events;
using ClipMate.Core.Services;
using ClipMate.Data.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Application = System.Windows.Application;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the collection/folder tree view.
/// Supports hierarchical structure: Database -> Collections -> Folders, plus Virtual Collections.
/// Sends CollectionNodeSelectedEvent via messenger when selection changes.
/// </summary>
public partial class CollectionTreeViewModel : ObservableObject
{
    private readonly ICollectionTreeBuilder _collectionTreeBuilder;
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<CollectionTreeViewModel> _logger;
    private readonly IMessenger _messenger;
    private readonly IClipRepositoryFactory _repositoryFactory;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    [ObservableProperty]
    private TreeNodeBase? _selectedNode;

    public CollectionTreeViewModel(IServiceScopeFactory serviceScopeFactory,
        IConfigurationService configurationService,
        IClipRepositoryFactory repositoryFactory,
        IMessenger messenger,
        ICollectionTreeBuilder collectionTreeBuilder,
        ILogger<CollectionTreeViewModel> logger)
    {
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _repositoryFactory = repositoryFactory ?? throw new ArgumentNullException(nameof(repositoryFactory));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _collectionTreeBuilder = collectionTreeBuilder ?? throw new ArgumentNullException(nameof(collectionTreeBuilder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Root nodes of the tree (typically Database nodes).
    /// </summary>
    public ObservableCollection<TreeNodeBase> RootNodes { get; } = [];

    /// <summary>
    /// Helper to create a scope and resolve a scoped service.
    /// </summary>
    private IServiceScope CreateScope() => _serviceScopeFactory.CreateScope();

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

        foreach (var node in treeNodes)
            RootNodes.Add(node);
    }

    /// <summary>
    /// Creates a new collection.
    /// </summary>
    [RelayCommand]
    private async Task CreateCollectionAsync((string name, string? description) parameters)
    {
        using var scope = CreateScope();
        var collectionService = scope.ServiceProvider.GetRequiredService<ICollectionService>();
        await collectionService.CreateAsync(parameters.name, parameters.description);
        await LoadAsync();
    }

    /// <summary>
    /// Creates a new folder.
    /// </summary>
    [RelayCommand]
    private async Task CreateFolderAsync((string name, Guid collectionId, Guid? parentFolderId) parameters)
    {
        using var scope = CreateScope();
        var folderService = scope.ServiceProvider.GetRequiredService<IFolderService>();
        await folderService.CreateAsync(parameters.name, parameters.collectionId, parameters.parentFolderId);
        await LoadAsync();
    }

    /// <summary>
    /// Deletes a collection.
    /// </summary>
    [RelayCommand]
    private async Task DeleteCollectionAsync(Guid collectionId)
    {
        using var scope = CreateScope();
        var collectionService = scope.ServiceProvider.GetRequiredService<ICollectionService>();
        await collectionService.DeleteAsync(collectionId);
        await LoadAsync();
    }

    /// <summary>
    /// Deletes a folder.
    /// </summary>
    [RelayCommand]
    private async Task DeleteFolderAsync(Guid folderId)
    {
        using var scope = CreateScope();
        var folderService = scope.ServiceProvider.GetRequiredService<IFolderService>();
        await folderService.DeleteAsync(folderId);
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
        using var scope = CreateScope();
        var collectionService = scope.ServiceProvider.GetRequiredService<ICollectionService>();

        var collection = await collectionService.GetByIdAsync(collectionId);
        if (collection == null)
        {
            _logger.LogWarning("Collection not found: {CollectionId}", collectionId);

            return;
        }

        // Get the active database key
        var activeDatabaseKey = collectionService.GetActiveDatabaseKey();

        var viewModel = new CollectionPropertiesViewModel(
            collection,
            _configurationService,
            false,
            scope.ServiceProvider,
            activeDatabaseKey);

        var window = new CollectionPropertiesWindow(viewModel, _configurationService)
        {
            Owner = Application.Current.GetDialogOwner(),
        };

        if (window.ShowDialog() == true)
        {
            // Sync SQL editor text to ViewModel before saving
            window.SyncSqlEditorToViewModel();
            viewModel.SaveToModel();

            // Save changes to database
            await collectionService.UpdateAsync(collection);
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

        using var scope = CreateScope();
        var dbManager = scope.ServiceProvider.GetRequiredService<IDatabaseManager>();
        var dbContext = dbManager.GetDatabaseContext(databaseKey);

        if (dbContext == null)
        {
            _logger.LogWarning("Cannot move collection: database context not found for {DatabaseKey}", databaseKey);
            return;
        }

        // Get all non-virtual collections in the same database, ordered by SortKey
        var collections = await dbContext.Collections
            .Where(p => !p.IsVirtual)
            .OrderBy(p => p.SortKey)
            .ToListAsync();

        var currentIndex = collections.FindIndex(p => p.Id == collectionNode.Collection.Id);
        if (currentIndex <= 0) // Already at top or not found
            return;

        // Swap SortKey with previous collection
        var previousCollection = collections[currentIndex - 1];
        (previousCollection.SortKey, collectionNode.Collection.SortKey) = (collectionNode.Collection.SortKey, previousCollection.SortKey);

        await dbContext.SaveChangesAsync();
        _logger.LogInformation("Moved collection {CollectionName} up (SortKey: {SortKey})",
            collectionNode.Collection.Name, collectionNode.Collection.SortKey);

        await LoadAsync(); // Reload tree to reflect new sort order
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

        using var scope = CreateScope();
        var dbManager = scope.ServiceProvider.GetRequiredService<IDatabaseManager>();
        var dbContext = dbManager.GetDatabaseContext(databaseKey);

        if (dbContext == null)
        {
            _logger.LogWarning("Cannot move collection: database context not found for {DatabaseKey}", databaseKey);
            return;
        }

        // Get all non-virtual collections in the same database, ordered by SortKey
        var collections = await dbContext.Collections
            .Where(p => !p.IsVirtual)
            .OrderBy(p => p.SortKey)
            .ToListAsync();

        var currentIndex = collections.FindIndex(p => p.Id == collectionNode.Collection.Id);
        if (currentIndex < 0 || currentIndex >= collections.Count - 1) // Already at bottom or not found
            return;

        // Swap SortKey with next collection
        var nextCollection = collections[currentIndex + 1];
        (nextCollection.SortKey, collectionNode.Collection.SortKey) = (collectionNode.Collection.SortKey, nextCollection.SortKey);

        await dbContext.SaveChangesAsync();
        _logger.LogInformation("Moved collection {CollectionName} down (SortKey: {SortKey})",
            collectionNode.Collection.Name, collectionNode.Collection.SortKey);

        await LoadAsync(); // Reload tree to reflect new sort order
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

        using var scope = CreateScope();
        var dbManager = scope.ServiceProvider.GetRequiredService<IDatabaseManager>();
        var dbContext = dbManager.GetDatabaseContext(databaseKey);

        if (dbContext == null)
        {
            _logger.LogWarning("Cannot reorder collections: database context not found for {DatabaseKey}", databaseKey);
            return;
        }

        // Get all non-virtual collections in the database, ordered by SortKey
        var allCollections = await dbContext.Collections
            .Where(p => !p.IsVirtual)
            .OrderBy(p => p.SortKey)
            .ToListAsync();

        // Remove dropped collections from current positions
        var droppedCollections = allCollections.Where(p => droppedCollectionIds.Contains(p.Id)).ToList();
        foreach (var item in droppedCollections)
            allCollections.Remove(item);

        // Find target collection index
        var targetIndex = allCollections.FindIndex(p => p.Id == targetCollectionId);
        if (targetIndex < 0)
        {
            _logger.LogWarning("Target collection not found: {TargetId}", targetCollectionId);
            return;
        }

        // Insert dropped collections at new position
        var insertIndex = insertAfter
            ? targetIndex + 1
            : targetIndex;

        allCollections.InsertRange(insertIndex, droppedCollections);

        // Reassign SortKey values based on new order
        for (var i = 0; i < allCollections.Count; i++)
            allCollections[i].SortKey = i;

        await dbContext.SaveChangesAsync();
        _logger.LogInformation("Reordered {Count} collections, inserted at position {Position}",
            droppedCollectionIds.Count, insertIndex);

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
        using var scope = _serviceScopeFactory.CreateScope();
        var databaseManager = scope.ServiceProvider.GetRequiredService<IDatabaseManager>();

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

        var dbContext = databaseManager.GetDatabaseContext(databaseNode.DatabasePath);
        if (dbContext == null)
        {
            _logger.LogError("Database context not found for path {Path}", databaseNode.DatabasePath);
            return;
        }

        // Use factory to create repository for the database
        var clipRepository = _repositoryFactory.CreateRepository(databaseNode.DatabasePath);
        await clipRepository.MoveClipsToCollectionAsync(clipIds, targetCollectionId);

        _logger.LogInformation("Moved {Count} clips to collection {CollectionId}", clipIds.Count, targetCollectionId);
    }

    /// <summary>
    /// Soft-deletes clips by setting Del=true (moves to Trashcan).
    /// </summary>
    public async Task SoftDeleteClipsAsync(List<Guid> clipIds, string databaseKey)
    {
        // Use factory to create repository for the database
        var clipRepository = _repositoryFactory.CreateRepository(databaseKey);
        await clipRepository.SoftDeleteClipsAsync(clipIds);

        _logger.LogInformation("Soft-deleted {Count} clips to Trashcan", clipIds.Count);
    }

    /// <summary>
    /// Restores clips from Trashcan by setting Del=false and moving to target collection.
    /// </summary>
    public async Task RestoreClipsAsync(List<Guid> clipIds, Guid targetCollectionId, string databaseKey)
    {
        // Use factory to create repository for the database
        var clipRepository = _repositoryFactory.CreateRepository(databaseKey);
        await clipRepository.RestoreClipsAsync(clipIds, targetCollectionId);

        _logger.LogInformation("Restored {Count} clips from Trashcan to collection {CollectionId}", clipIds.Count, targetCollectionId);
    }

    /// <summary>
    /// Walks up the tree to find the DatabaseTreeNode ancestor.
    /// </summary>
    private DatabaseTreeNode? GetDatabaseNode(TreeNodeBase node)
    {
        // Walk up the tree to find the database node
        foreach (var root in RootNodes)
        {
            if (root is DatabaseTreeNode dbNode && IsDescendantOf(node, dbNode))
                return dbNode;
        }

        return null;
    }

    /// <summary>
    /// Checks if a node is a descendant of a potential ancestor.
    /// </summary>
    private bool IsDescendantOf(TreeNodeBase node, TreeNodeBase potentialAncestor) => node == potentialAncestor || potentialAncestor.Children.Any(p => IsDescendantOf(node, p));
}
