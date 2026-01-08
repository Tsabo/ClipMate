using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ClipMate.App.Models.TreeNodes;
using ClipMate.App.Services;
using ClipMate.App.ViewModels;
using DevExpress.Xpf.Grid;
using Microsoft.Extensions.DependencyInjection;

namespace ClipMate.App.Views.Dialogs;

/// <summary>
/// Dialog for selecting a target collection for Copy/Move operations.
/// </summary>
public partial class CollectionPickerDialog : INotifyPropertyChanged
{
    private readonly ICollectionTreeBuilder _collectionTreeBuilder;

    public CollectionPickerDialog(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        DataContext = this;
        RootNodes = [];

        _collectionTreeBuilder = serviceProvider.GetRequiredService<ICollectionTreeBuilder>();
    }

    /// <summary>
    /// Database nodes with their collections.
    /// </summary>
    public ObservableCollection<TreeNodeBase> RootNodes => field;

    /// <summary>
    /// Message to display to the user.
    /// </summary>
    public string Message
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            OnPropertyChanged();
        }
    } = "Select a target collection:";

    /// <summary>
    /// Currently selected collection.
    /// </summary>
    public CollectionTreeNode? SelectedCollection
    {
        get;
        private set
        {
            if (field == value)
                return;

            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(SelectedDatabaseKey));
        }
    }

    /// <summary>
    /// Gets whether a collection is selected.
    /// </summary>
    public bool HasSelection => SelectedCollection is not null;

    /// <summary>
    /// Gets the selected collection ID, or null if no selection.
    /// </summary>
    public Guid? SelectedCollectionId => SelectedCollection?.Collection.Id;

    /// <summary>
    /// Gets the database key for the selected collection by traversing up the tree.
    /// </summary>
    public string? SelectedDatabaseKey
    {
        get
        {
            if (SelectedCollection == null)
                return null;

            // Traverse up the tree to find the DatabaseTreeNode
            var current = SelectedCollection.Parent;
            while (current != null)
            {
                if (current is DatabaseTreeNode dbNode)
                    return dbNode.DatabasePath;

                current = current.Parent;
            }

            return null;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Loads all collections from all databases as a tree structure.
    /// </summary>
    public async Task LoadCollectionsAsync(CancellationToken cancellationToken = default)
    {
        RootNodes.Clear();
        var treeNodes = await _collectionTreeBuilder.BuildTreeAsync(
            TreeNodeType.VirtualCollection | TreeNodeType.SpecialCollection,cancellationToken);
        foreach (var item in treeNodes)
            RootNodes.Add(item);
    }

    private void CollectionsTreeView_SelectedItemChanged(object sender, SelectedItemChangedEventArgs e)
    {
        // Only allow selecting Collection nodes, not DatabaseNode
        SelectedCollection = e.NewItem as CollectionTreeNode;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedCollection is null)
        {
            DialogResult = false;
            return;
        }

        DialogResult = true;
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
