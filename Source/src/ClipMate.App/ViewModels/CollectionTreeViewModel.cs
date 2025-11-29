using System.Collections.ObjectModel;
using ClipMate.App.Views;
using ClipMate.Core.Events;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
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
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<CollectionTreeViewModel> _logger;
    private readonly IMessenger _messenger;

    [ObservableProperty]
    private TreeNodeBase? _selectedNode;

    public CollectionTreeViewModel(IServiceScopeFactory serviceScopeFactory,
        IConfigurationService configurationService,
        IMessenger messenger,
        ILogger<CollectionTreeViewModel> logger)
    {
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Helper to create a scope and resolve a scoped service.
    /// </summary>
    private IServiceScope CreateScope() => _serviceScopeFactory.CreateScope();

    /// <summary>
    /// Root nodes of the tree (typically Database nodes).
    /// </summary>
    public ObservableCollection<TreeNodeBase> RootNodes { get; } = new();

    partial void OnSelectedNodeChanged(TreeNodeBase? value)
    {
        _logger.LogInformation("Selection changed: NodeType={NodeType}", value?.GetType().Name ?? "null");

        // Send messenger event with collection/folder IDs based on node type
        switch (value)
        {
            case CollectionTreeNode collectionNode:
                _logger.LogInformation("Sending CollectionNodeSelectedEvent: CollectionId={CollectionId}, FolderId=null", collectionNode.Collection.Id);
                _messenger.Send(new CollectionNodeSelectedEvent(collectionNode.Collection.Id, null));
                break;

            case FolderTreeNode folderNode:
                _logger.LogInformation("Sending CollectionNodeSelectedEvent: CollectionId={CollectionId}, FolderId={FolderId}", folderNode.Folder.CollectionId, folderNode.Folder.Id);
                _messenger.Send(new CollectionNodeSelectedEvent(folderNode.Folder.CollectionId, folderNode.Folder.Id));
                break;

            case VirtualCollectionTreeNode virtualNode:
                _logger.LogInformation("Sending CollectionNodeSelectedEvent: CollectionId={CollectionId}, FolderId=null", virtualNode.VirtualCollection.Id);
                _messenger.Send(new CollectionNodeSelectedEvent(virtualNode.VirtualCollection.Id, null));
                break;

            // Database and VirtualCollectionsContainer nodes don't trigger selection changes
        }
    }

    /// <summary>
    /// Loads the complete tree hierarchy: Database -> Collections/Virtual Collections -> Folders.
    /// Creates a database node for each configured database.
    /// </summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        RootNodes.Clear();

        // Load configuration to get database definitions
        var configuration = _configurationService.Configuration;

        // Load all collections (currently from the active database)
        IReadOnlyCollection<Core.Models.Collection> allCollections;
        using (var scope = _serviceScopeFactory.CreateScope())
        {
            var collectionService = scope.ServiceProvider.GetRequiredService<ICollectionService>();
            allCollections = await collectionService.GetAllAsync(cancellationToken);
        }

        // Separate regular collections from virtual ones
        var regularCollections = allCollections.Where(p => !p.IsVirtual).ToList();
        var virtualCollections = allCollections.Where(p => p.IsVirtual).ToList();

        // Create a database node for each configured database
        if (configuration.Databases.Any())
        {
            foreach (var dbEntry in configuration.Databases)
            {
                var databaseId = dbEntry.Key;
                var databaseConfig = dbEntry.Value;

                // Create database node with title from configuration
                var databaseNode = new DatabaseTreeNode(databaseConfig.Name, databaseId);

                // Only load collections for the default/active database
                // TODO: In the future, support loading collections from multiple databases
                if (databaseId == configuration.DefaultDatabase)
                {
                    // Add regular collections to database node
                    foreach (var collection in regularCollections.OrderBy(p => p.SortKey))
                    {
                        var collectionNode = new CollectionTreeNode(collection);
                        await LoadFoldersAsync(collectionNode, cancellationToken);

                        databaseNode.Children.Add(collectionNode);
                    }

                    // Add virtual collections container if any virtual collections exist
                    if (virtualCollections.Any())
                    {
                        var virtualContainer = new VirtualCollectionsContainerNode();

                        foreach (var virtualCollection in virtualCollections.OrderBy(p => p.SortKey))
                        {
                            var virtualNode = new VirtualCollectionTreeNode(virtualCollection);
                            virtualContainer.Children.Add(virtualNode);
                        }

                        databaseNode.Children.Add(virtualContainer);
                    }

                    // Expand the active database node by default
                    databaseNode.IsExpanded = true;
                }

                RootNodes.Add(databaseNode);
            }
        }
        else
        {
            // Fallback: If no databases configured, create a default node
            var databaseNode = new DatabaseTreeNode("My Clips", "default");

            // Add regular collections to database node
            foreach (var collection in regularCollections.OrderBy(p => p.SortKey))
            {
                var collectionNode = new CollectionTreeNode(collection);
                await LoadFoldersAsync(collectionNode, cancellationToken);

                databaseNode.Children.Add(collectionNode);
            }

            // Add virtual collections container if any virtual collections exist
            if (virtualCollections.Any())
            {
                var virtualContainer = new VirtualCollectionsContainerNode();

                foreach (var virtualCollection in virtualCollections.OrderBy(p => p.SortKey))
                {
                    var virtualNode = new VirtualCollectionTreeNode(virtualCollection);
                    virtualContainer.Children.Add(virtualNode);
                }

                databaseNode.Children.Add(virtualContainer);
            }

            RootNodes.Add(databaseNode);
            databaseNode.IsExpanded = true;
        }
    }

    /// <summary>
    /// Loads the folder hierarchy for a collection.
    /// </summary>
    private async Task LoadFoldersAsync(CollectionTreeNode collectionNode, CancellationToken cancellationToken)
    {
        using var scope = CreateScope();
        var folderService = scope.ServiceProvider.GetRequiredService<IFolderService>();
        var rootFolders = await folderService.GetRootFoldersAsync(collectionNode.Collection.Id, cancellationToken);

        foreach (var folder in rootFolders)
        {
            var folderNode = new FolderTreeNode(folder);
            await LoadSubFoldersAsync(folderNode, cancellationToken);

            collectionNode.Children.Add(folderNode);
        }
    }

    /// <summary>
    /// Recursively loads subfolders for a folder node.
    /// </summary>
    private async Task LoadSubFoldersAsync(FolderTreeNode folderNode, CancellationToken cancellationToken)
    {
        using var scope = CreateScope();
        var folderService = scope.ServiceProvider.GetRequiredService<IFolderService>();
        var subFolders = await folderService.GetChildFoldersAsync(folderNode.Folder.Id, cancellationToken);

        foreach (var subFolder in subFolders)
        {
            var subFolderNode = new FolderTreeNode(subFolder);
            await LoadSubFoldersAsync(subFolderNode, cancellationToken);

            folderNode.Children.Add(subFolderNode);
        }
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

        var viewModel = new CollectionPropertiesViewModel(collection, _configurationService);
        var window = new CollectionPropertiesWindow(viewModel, _configurationService)
        {
            Owner = Application.Current.MainWindow,
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
}
