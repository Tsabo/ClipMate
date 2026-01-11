using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ClipMate.App.Models.TreeNodes;
using ClipMate.App.Services;
using ClipMate.Core.Services;
using DevExpress.Xpf.Grid;
using Microsoft.Extensions.DependencyInjection;

namespace ClipMate.App.Views.Dialogs;

/// <summary>
/// Dialog for selecting a target collection for Copy/Move operations.
/// </summary>
public partial class CollectionPickerDialog : INotifyPropertyChanged
{
    // Static fields to track last selected target across dialog invocations (in-memory only)
    private static Guid? _sLastSelectedCollectionId;
    private static string? _sLastSelectedDatabaseKey;
    private static DateTime? _sLastSelectionTimestamp;
    private static readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

    private readonly ICollectionTreeBuilder _collectionTreeBuilder;
    private readonly IConfigurationService _configurationService;

    public CollectionPickerDialog(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        DataContext = this;
        RootNodes = [];

        _collectionTreeBuilder = serviceProvider.GetRequiredService<ICollectionTreeBuilder>();
        _configurationService = serviceProvider.GetRequiredService<IConfigurationService>();
    }

    /// <summary>
    /// Database nodes with their collections.
    /// </summary>
    public ObservableCollection<TreeNodeBase> RootNodes { get; }

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
            TreeNodeType.VirtualCollection | TreeNodeType.SpecialCollection, cancellationToken);

        foreach (var item in treeNodes)
            RootNodes.Add(item);

        // If ReuseLastSelectedMoveTarget is enabled, try to pre-select the last used collection
        var preferences = _configurationService.Configuration.Preferences;
        if (preferences.ReuseLastSelectedMoveTarget &&
            _sLastSelectedCollectionId.HasValue &&
            !string.IsNullOrEmpty(_sLastSelectedDatabaseKey) &&
            _sLastSelectionTimestamp.HasValue &&
            DateTime.UtcNow - _sLastSelectionTimestamp.Value < _cacheExpiration)
            TrySelectLastUsedCollection(_sLastSelectedDatabaseKey, _sLastSelectedCollectionId.Value);
    }

    /// <summary>
    /// Attempts to find and select the last used collection in the tree.
    /// </summary>
    private void TrySelectLastUsedCollection(string databaseKey, Guid collectionId)
    {
        foreach (var rootNode in RootNodes)
        {
            if (rootNode is not DatabaseTreeNode dbNode || dbNode.DatabasePath != databaseKey)
                continue;

            var collectionNode = FindCollectionNodeById(dbNode, collectionId);
            if (collectionNode == null)
                continue;

            SelectedCollection = collectionNode;
            // Expand the path to the selected node
            ExpandPathToNode(collectionNode);
            break;
        }
    }

    /// <summary>
    /// Recursively finds a collection node by ID.
    /// </summary>
    private CollectionTreeNode? FindCollectionNodeById(TreeNodeBase node, Guid collectionId)
    {
        if (node is CollectionTreeNode collectionNode && collectionNode.Collection.Id == collectionId)
            return collectionNode;

        foreach (var item in node.Children)
        {
            var found = FindCollectionNodeById(item, collectionId);
            if (found != null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// Expands all parent nodes to make the target node visible.
    /// </summary>
    private void ExpandPathToNode(TreeNodeBase node)
    {
        var pathNodes = new List<TreeNodeBase>();
        var current = node.Parent;

        while (current != null)
        {
            pathNodes.Add(current);
            current = current.Parent;
        }

        // Expand from root to target
        pathNodes.Reverse();
        foreach (var pathNode in pathNodes)
            pathNode.IsExpanded = true;
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

        // Save last selected collection if ReuseLastSelectedMoveTarget is enabled
        var preferences = _configurationService.Configuration.Preferences;
        if (preferences.ReuseLastSelectedMoveTarget)
        {
            _sLastSelectedCollectionId = SelectedCollection.Collection.Id;
            _sLastSelectedDatabaseKey = SelectedDatabaseKey;
            _sLastSelectionTimestamp = DateTime.UtcNow;
        }

        DialogResult = true;
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
