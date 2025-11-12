using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClipMate.Core.Models;
using ClipMate.Core.Services;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the collection/folder tree view.
/// </summary>
public partial class CollectionTreeViewModel : ObservableObject
{
    private readonly ICollectionService _collectionService;
    private readonly IFolderService _folderService;

    [ObservableProperty]
    private object? _selectedNode;

    /// <summary>
    /// Collection of all collections with their folder hierarchies.
    /// </summary>
    public ObservableCollection<CollectionTreeNode> Collections { get; } = new();

    public CollectionTreeViewModel(ICollectionService collectionService, IFolderService folderService)
    {
        _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
        _folderService = folderService ?? throw new ArgumentNullException(nameof(folderService));
    }

    /// <summary>
    /// Loads all collections and their folder hierarchies.
    /// </summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        Collections.Clear();

        var collections = await _collectionService.GetAllAsync(cancellationToken);

        foreach (var collection in collections)
        {
            var collectionNode = new CollectionTreeNode(collection);
            await LoadFoldersAsync(collectionNode, cancellationToken);
            Collections.Add(collectionNode);
        }
    }

    /// <summary>
    /// Loads the folder hierarchy for a collection.
    /// </summary>
    private async Task LoadFoldersAsync(CollectionTreeNode collectionNode, CancellationToken cancellationToken)
    {
        var rootFolders = await _folderService.GetRootFoldersAsync(collectionNode.Collection.Id, cancellationToken);

        foreach (var folder in rootFolders)
        {
            var folderNode = new FolderTreeNode(folder);
            await LoadSubFoldersAsync(folderNode, cancellationToken);
            collectionNode.Folders.Add(folderNode);
        }
    }

    /// <summary>
    /// Recursively loads subfolders for a folder node.
    /// </summary>
    private async Task LoadSubFoldersAsync(FolderTreeNode folderNode, CancellationToken cancellationToken)
    {
        var subFolders = await _folderService.GetChildFoldersAsync(folderNode.Folder.Id, cancellationToken);

        foreach (var subFolder in subFolders)
        {
            var subFolderNode = new FolderTreeNode(subFolder);
            await LoadSubFoldersAsync(subFolderNode, cancellationToken);
            folderNode.SubFolders.Add(subFolderNode);
        }
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
}
