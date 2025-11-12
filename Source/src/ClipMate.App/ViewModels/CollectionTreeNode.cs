using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ClipMate.Core.Models;

namespace ClipMate.App.ViewModels;

/// <summary>
/// Represents a collection node in the tree view.
/// </summary>
public partial class CollectionTreeNode : ObservableObject
{
    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// The underlying collection model.
    /// </summary>
    public Collection Collection { get; }

    /// <summary>
    /// Display name for the collection.
    /// </summary>
    public string Name => Collection.Name;

    /// <summary>
    /// Collection of root folders in this collection.
    /// </summary>
    public ObservableCollection<FolderTreeNode> Folders { get; } = new();

    public CollectionTreeNode(Collection collection)
    {
        Collection = collection ?? throw new ArgumentNullException(nameof(collection));
    }
}

/// <summary>
/// Represents a folder node in the tree view.
/// </summary>
public partial class FolderTreeNode : ObservableObject
{
    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// The underlying folder model.
    /// </summary>
    public Folder Folder { get; }

    /// <summary>
    /// Display name for the folder.
    /// </summary>
    public string Name => Folder.Name;

    /// <summary>
    /// Collection of subfolders.
    /// </summary>
    public ObservableCollection<FolderTreeNode> SubFolders { get; } = new();

    public FolderTreeNode(Folder folder)
    {
        Folder = folder ?? throw new ArgumentNullException(nameof(folder));
    }
}
