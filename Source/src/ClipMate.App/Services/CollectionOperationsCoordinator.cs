using ClipMate.App.Models.TreeNodes;
using ClipMate.App.ViewModels;
using ClipMate.App.Views.Dialogs;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using ClipMate.Data.Services;
using CommunityToolkit.Mvvm.Messaging;
using DevExpress.Xpf.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Application = System.Windows.Application;

namespace ClipMate.App.Services;

/// <summary>
/// Coordinates collection operations (add, delete, reload, resequence) across all windows.
/// Also handles navigation commands (switch to last/favorite collection).
/// </summary>
public class CollectionOperationsCoordinator :
    IRecipient<AddCollectionRequestedEvent>,
    IRecipient<DeleteCollectionRequestedEvent>,
    IRecipient<ReloadCollectionRequestedEvent>,
    IRecipient<ResequenceSortKeysRequestedEvent>,
    IRecipient<SwitchToLastCollectionRequestedEvent>,
    IRecipient<SwitchToFavoriteCollectionRequestedEvent>,
    IRecipient<RestoreClipsRequestedEvent>,
    IRecipient<SelectAllClipsRequestedEvent>,
    IRecipient<AppendClipsRequestedEvent>,
    IRecipient<ActivateDatabaseRequestedEvent>,
    IRecipient<DeactivateDatabaseRequestedEvent>,
    IRecipient<MoveToNamedCollectionRequestedEvent>,
    IRecipient<SelectNamedCollectionRequestedEvent>,
    IRecipient<ShowCollectionPropertiesRequestedEvent>
{
    private readonly IActiveWindowService _activeWindowService;
    private readonly IClipAppendService _clipAppendService;
    private readonly ClipListViewModel _clipListViewModel;
    private readonly IClipService _clipService;
    private readonly ICollectionService _collectionService;
    private readonly CollectionTreeViewModel _collectionTreeViewModel;
    private readonly IConfigurationService _configurationService;
    private readonly IDatabaseManager _databaseManager;
    private readonly ILogger<CollectionOperationsCoordinator> _logger;
    private readonly IMessenger _messenger;
    private readonly IServiceProvider _serviceProvider;

    // Navigation state
    private TreeNodeBase? _lastSelectedNode;

    public CollectionOperationsCoordinator(IActiveWindowService activeWindowService,
        ClipListViewModel clipListViewModel,
        CollectionTreeViewModel collectionTreeViewModel,
        IClipService clipService,
        IClipAppendService clipAppendService,
        ICollectionService collectionService,
        IConfigurationService configurationService,
        IDatabaseManager databaseManager,
        IMessenger messenger,
        IServiceProvider serviceProvider,
        ILogger<CollectionOperationsCoordinator> logger)
    {
        _activeWindowService = activeWindowService ?? throw new ArgumentNullException(nameof(activeWindowService));
        _clipListViewModel = clipListViewModel ?? throw new ArgumentNullException(nameof(clipListViewModel));
        _collectionTreeViewModel = collectionTreeViewModel ?? throw new ArgumentNullException(nameof(collectionTreeViewModel));
        _clipService = clipService ?? throw new ArgumentNullException(nameof(clipService));
        _clipAppendService = clipAppendService ?? throw new ArgumentNullException(nameof(clipAppendService));
        _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _databaseManager = databaseManager ?? throw new ArgumentNullException(nameof(databaseManager));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Register for all collection operation events
        _messenger.Register<AddCollectionRequestedEvent>(this);
        _messenger.Register<DeleteCollectionRequestedEvent>(this);
        _messenger.Register<ReloadCollectionRequestedEvent>(this);
        _messenger.Register<ResequenceSortKeysRequestedEvent>(this);
        _messenger.Register<SwitchToLastCollectionRequestedEvent>(this);
        _messenger.Register<SwitchToFavoriteCollectionRequestedEvent>(this);
        _messenger.Register<RestoreClipsRequestedEvent>(this);
        _messenger.Register<SelectAllClipsRequestedEvent>(this);
        _messenger.Register<AppendClipsRequestedEvent>(this);
        _messenger.Register<ActivateDatabaseRequestedEvent>(this);
        _messenger.Register<DeactivateDatabaseRequestedEvent>(this);
        _messenger.Register<MoveToNamedCollectionRequestedEvent>(this);
        _messenger.Register<SelectNamedCollectionRequestedEvent>(this);
        _messenger.Register<ShowCollectionPropertiesRequestedEvent>(this);

        // Track selection changes for "Switch to Last Collection" feature
        // Subscribe to the ViewModel's PropertyChanged to capture the previous selection before it changes
        _collectionTreeViewModel.PropertyChanging += (_, e) =>
        {
            if (e.PropertyName == nameof(CollectionTreeViewModel.SelectedNode) && _collectionTreeViewModel.SelectedNode != null)
                _lastSelectedNode = _collectionTreeViewModel.SelectedNode;
        };

        _logger.LogDebug("CollectionOperationsCoordinator initialized and registered for events");
    }

    /// <summary>
    /// Handles ActivateDatabaseRequestedEvent to load a database.
    /// </summary>
    public async void Receive(ActivateDatabaseRequestedEvent message)
    {
        try
        {
            var loaded = await _databaseManager.LoadDatabaseAsync(message.DatabaseKey);
            if (loaded)
            {
                _logger.LogInformation("Activated database: {DatabaseKey}", message.DatabaseKey);
                SendStatus("Database activated");
                _messenger.Send(new RefreshCollectionTreeRequestedEvent());
            }
            else
                SendStatus("Failed to activate database", true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate database: {DatabaseKey}", message.DatabaseKey);
            SendStatus($"Failed to activate database: {ex.Message}", true);
        }
    }

    /// <summary>
    /// Handles AddCollectionRequestedEvent to create a new collection.
    /// </summary>
    public async void Receive(AddCollectionRequestedEvent message)
    {
        var databaseKey = GetDatabaseKeyForSelectedNode();
        if (string.IsNullOrEmpty(databaseKey))
        {
            SendStatus("No database selected");
            return;
        }

        // Get currently selected collection name for the dialog
        string? selectedCollectionName = null;
        Guid? parentId = null;

        if (_collectionTreeViewModel.SelectedNode is CollectionTreeNode selectedCollectionNode)
        {
            selectedCollectionName = selectedCollectionNode.Collection.Name;
            parentId = selectedCollectionNode.Collection.Id;
        }
        else if (_collectionTreeViewModel.SelectedNode is FolderTreeNode folderNode)
        {
            selectedCollectionName = folderNode.Folder.Name;
            parentId = folderNode.Folder.CollectionId;
        }

        // Show the Add Collection dialog
        var dialog = new AddCollectionDialog(selectedCollectionName)
        {
            Owner = _activeWindowService.DialogOwner,
        };

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.CollectionName))
            return;

        try
        {
            // Determine parent based on user's positioning choice
            Guid? effectiveParentId = dialog.PositionBelowSelected
                ? parentId
                : null;

            var collection = await _collectionService.CreateAsync(dialog.CollectionName, effectiveParentId, databaseKey);

            _logger.LogInformation("Created new collection: {Name} (ID: {Id})", collection.Name, collection.Id);
            SendStatus($"Created collection: {collection.Name}");

            // Refresh the collection tree
            _messenger.Send(new RefreshCollectionTreeRequestedEvent());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create collection");
            SendStatus($"Failed to create collection: {ex.Message}", true);
        }
    }

    /// <summary>
    /// Handles AppendClipsRequestedEvent to glue selected text clips together.
    /// </summary>
    public async void Receive(AppendClipsRequestedEvent message)
    {
        var selectedClips = _clipListViewModel.SelectedClips.ToList();

        if (selectedClips.Count < 2)
        {
            SendStatus("Select at least 2 clips to append");
            return;
        }

        // Verify all clips are text-based
        var nonTextClips = selectedClips.Where(p => p.Type != ClipType.Text &&
                                                    p.Type != ClipType.RichText)
            .ToList();

        if (nonTextClips.Count > 0)
        {
            DXMessageBox.Show(
                _activeWindowService.DialogOwner,
                "Append (Glue) is only available for text clips.",
                "Append Clips",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        try
        {
            var preferences = _configurationService.Configuration.Preferences;
            var separator = preferences.AppendSeparatorString;
            var stripTrailing = preferences.StripTrailingLineBreak;

            var resultClip = await _clipAppendService.AppendClipsAsync(selectedClips, separator, stripTrailing);

            _logger.LogInformation("Appended {Count} clips into new clip {ClipId}", selectedClips.Count, resultClip.Id);
            SendStatus($"Appended {selectedClips.Count} clips");

            // Refresh the clip list
            _messenger.Send(new ClipUpdatedMessage(resultClip.Id, resultClip.Title));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to append clips");
            SendStatus($"Failed to append: {ex.Message}", true);
        }
    }

    /// <summary>
    /// Handles DeactivateDatabaseRequestedEvent to unload a database.
    /// </summary>
    public void Receive(DeactivateDatabaseRequestedEvent message)
    {
        try
        {
            var unloaded = _databaseManager.UnloadDatabase(message.DatabaseKey);
            if (unloaded)
            {
                _logger.LogInformation("Deactivated database: {DatabaseKey}", message.DatabaseKey);
                SendStatus("Database deactivated");
                _messenger.Send(new RefreshCollectionTreeRequestedEvent());
            }
            else
                SendStatus("Failed to deactivate database", true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deactivate database: {DatabaseKey}", message.DatabaseKey);
            SendStatus($"Failed to deactivate database: {ex.Message}", true);
        }
    }

    /// <summary>
    /// Handles DeleteCollectionRequestedEvent to delete the selected collection or virtual collection.
    /// </summary>
    public async void Receive(DeleteCollectionRequestedEvent message)
    {
        Collection collection;
        string nodeType;

        if (_collectionTreeViewModel.SelectedNode is CollectionTreeNode collectionNode)
        {
            collection = collectionNode.Collection;
            nodeType = "collection";
        }
        else if (_collectionTreeViewModel.SelectedNode is VirtualCollectionTreeNode virtualNode)
        {
            collection = virtualNode.VirtualCollection;
            nodeType = "virtual collection";
        }
        else
        {
            SendStatus("No collection selected");
            return;
        }

        // Don't allow deleting special collections
        if (collection.IsSpecial)
        {
            DXMessageBox.Show(
                _activeWindowService.DialogOwner,
                "Cannot delete special collections (Trash Can, etc.).",
                "Delete Collection",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        // Confirm deletion
        var dialogMessage = nodeType == "virtual collection"
            ? $"Are you sure you want to delete the virtual collection '{collection.Name}'?\n\nThe saved query will be deleted, but the actual clips will remain in their original collections."
            : $"Are you sure you want to delete the collection '{collection.Name}'?\n\nAll clips in this collection will be permanently deleted.";

        var result = DXMessageBox.Show(
            _activeWindowService.DialogOwner,
            dialogMessage,
            "Delete Collection",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            await _collectionService.DeleteAsync(collection.Id);

            _logger.LogInformation("Deleted {NodeType}: {Name} (ID: {Id})", nodeType, collection.Name, collection.Id);
            SendStatus($"Deleted {nodeType}: {collection.Name}");

            // Refresh the collection tree
            _messenger.Send(new RefreshCollectionTreeRequestedEvent());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete collection: {CollectionId}", collection.Id);
            SendStatus($"Failed to delete collection: {ex.Message}", true);
        }
    }

    /// <summary>
    /// Handles MoveToNamedCollectionRequestedEvent to move selected clips to a named collection.
    /// </summary>
    public async void Receive(MoveToNamedCollectionRequestedEvent message)
    {
        var selectedClips = _clipListViewModel.SelectedClips.ToList();

        if (selectedClips.Count == 0)
        {
            SendStatus("No clips selected");
            return;
        }

        var sourceDatabaseKey = GetDatabaseKeyForSelectedNode();
        if (string.IsNullOrEmpty(sourceDatabaseKey))
        {
            SendStatus("No database selected");
            return;
        }

        try
        {
            // Find the target collection by name
            var targetCollection = await _collectionService.GetByNameAsync(message.CollectionName, sourceDatabaseKey);
            if (targetCollection == null)
            {
                SendStatus($"Collection '{message.CollectionName}' not found");
                return;
            }

            foreach (var item in selectedClips)
                await _clipService.MoveClipAsync(sourceDatabaseKey, item.Id, targetCollection.Id);

            _logger.LogInformation("Moved {Count} clips to collection {CollectionName}", selectedClips.Count, message.CollectionName);
            SendStatus($"Moved {selectedClips.Count} clip(s) to {message.CollectionName}");

            // Refresh the clip list
            _messenger.Send(new ReloadClipsRequestedEvent());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move clips to {CollectionName}", message.CollectionName);
            SendStatus($"Failed to move clips: {ex.Message}", true);
        }
    }

    /// <summary>
    /// Handles ReloadCollectionRequestedEvent to refresh clips from the database.
    /// </summary>
    public void Receive(ReloadCollectionRequestedEvent message)
    {
        if (_collectionTreeViewModel.SelectedNode is CollectionTreeNode collectionNode)
        {
            var databaseKey = GetDatabaseKeyForSelectedNode();
            if (string.IsNullOrEmpty(databaseKey))
                return;

            // Trigger a reload of the current clip list
            _messenger.Send(new ReloadClipsRequestedEvent());
            SendStatus($"Reloaded collection: {collectionNode.Collection.Name}");
        }
        else
            SendStatus("No collection selected");
    }

    /// <summary>
    /// Handles ResequenceSortKeysRequestedEvent to normalize collection sort keys.
    /// </summary>
    public async void Receive(ResequenceSortKeysRequestedEvent message)
    {
        var databaseKey = GetDatabaseKeyForSelectedNode();
        if (string.IsNullOrEmpty(databaseKey))
        {
            SendStatus("No database selected");
            return;
        }

        try
        {
            var count = await _collectionService.ResequenceSortKeysAsync(databaseKey);

            _logger.LogInformation("Resequenced sort keys for {Count} collections in database {DatabaseKey}", count, databaseKey);
            SendStatus($"Resequenced {count} collections");

            // Refresh the collection tree
            _messenger.Send(new RefreshCollectionTreeRequestedEvent());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resequence sort keys");
            SendStatus($"Failed to resequence: {ex.Message}", true);
        }
    }

    /// <summary>
    /// Handles RestoreClipsRequestedEvent to restore clips from trash.
    /// </summary>
    public async void Receive(RestoreClipsRequestedEvent message)
    {
        var selectedClips = _clipListViewModel.SelectedClips;

        if (selectedClips.Count == 0)
        {
            SendStatus("No clips selected");
            return;
        }

        var sourceDatabaseKey = GetDatabaseKeyForSelectedNode();
        if (string.IsNullOrEmpty(sourceDatabaseKey))
        {
            SendStatus("No database selected");
            return;
        }

        // Show collection picker dialog
        var dialog = _serviceProvider.GetRequiredService<CollectionPickerDialog>();
        dialog.Owner = _activeWindowService.DialogOwner;
        dialog.Title = "Restore to Collection";

        if (dialog.ShowDialog() != true || dialog.SelectedCollection == null)
            return;

        try
        {
            var targetCollectionId = dialog.SelectedCollection.Collection.Id;
            var targetDatabaseKey = dialog.SelectedDatabaseKey ?? sourceDatabaseKey;

            foreach (var item in selectedClips.ToList())
            {
                // Restore from trash by moving clip back to the target collection
                if (sourceDatabaseKey == targetDatabaseKey)
                    await _clipService.MoveClipAsync(sourceDatabaseKey, item.Id, targetCollectionId);
                else
                    await _clipService.MoveClipCrossDatabaseAsync(sourceDatabaseKey, item.Id, targetDatabaseKey, targetCollectionId);
            }

            _logger.LogInformation("Restored {Count} clips to collection {CollectionId}", selectedClips.Count, targetCollectionId);
            SendStatus($"Restored {selectedClips.Count} clip(s)");

            // Refresh the clip list - after restore, trigger a reload of the current collection
            var firstClip = selectedClips.First();
            _messenger.Send(new ClipUpdatedMessage(firstClip.Id, firstClip.Title));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore clips");
            SendStatus($"Failed to restore: {ex.Message}", true);
        }
    }

    /// <summary>
    /// Handles SelectAllClipsRequestedEvent to select all clips in the current list.
    /// </summary>
    public void Receive(SelectAllClipsRequestedEvent message)
    {
        _clipListViewModel.SelectedClips.Clear();
        foreach (var item in _clipListViewModel.Clips)
            _clipListViewModel.SelectedClips.Add(item);

        SendStatus($"Selected {_clipListViewModel.Clips.Count} clip(s)");
    }

    /// <summary>
    /// Handles SelectNamedCollectionRequestedEvent to select a named collection in the tree.
    /// </summary>
    public async void Receive(SelectNamedCollectionRequestedEvent message)
    {
        var databaseKey = GetDatabaseKeyForSelectedNode();
        if (string.IsNullOrEmpty(databaseKey))
        {
            // Use first loaded database if none selected
            var databaseManager = _serviceProvider.GetRequiredService<IDatabaseManager>();
            var firstDb = databaseManager.GetLoadedDatabases().FirstOrDefault();
            if (firstDb == null)
            {
                SendStatus("No database loaded");
                return;
            }

            databaseKey = firstDb.FilePath;
        }

        try
        {
            // Find the target collection by name
            var targetCollection = await _collectionService.GetByNameAsync(message.CollectionName, databaseKey);
            if (targetCollection == null)
            {
                SendStatus($"Collection '{message.CollectionName}' not found");
                return;
            }

            // Find and select the node in the tree
            var node = FindCollectionNode(_collectionTreeViewModel.RootNodes, targetCollection.Id);
            if (node != null)
            {
                _collectionTreeViewModel.SelectedNode = node;
                _logger.LogDebug("Selected collection: {CollectionName}", message.CollectionName);
            }
            else
                SendStatus($"Collection '{message.CollectionName}' not found in tree");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to select collection {CollectionName}", message.CollectionName);
            SendStatus($"Failed to select collection: {ex.Message}", true);
        }
    }

    /// <summary>
    /// Handles the request to show the collection properties dialog for the selected collection.
    /// </summary>
    public async void Receive(ShowCollectionPropertiesRequestedEvent message)
    {
        var selectedNode = _collectionTreeViewModel.SelectedNode;

        if (selectedNode is not CollectionTreeNode collectionNode)
        {
            _logger.LogDebug("ShowCollectionProperties: No collection selected");
            return;
        }

        try
        {
            var collection = await _collectionService.GetByIdAsync(collectionNode.Collection.Id);
            if (collection == null)
            {
                _logger.LogWarning("Collection not found: {CollectionId}", collectionNode.Collection.Id);
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

            if (window.ShowDialog() != true)
                return;

            // Sync SQL editor text to ViewModel before saving
            window.SyncSqlEditorToViewModel();
            viewModel.SaveToModel();

            // Save changes to database
            await _collectionService.UpdateAsync(collection);
            await _collectionTreeViewModel.LoadAsync(); // Reload tree to reflect changes
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show collection properties for {CollectionId}", collectionNode.Collection.Id);
            SendStatus($"Failed to show collection properties: {ex.Message}", true);
        }
    }

    /// <summary>
    /// Handles SwitchToFavoriteCollectionRequestedEvent to navigate to the favorite collection.
    /// </summary>
    public async void Receive(SwitchToFavoriteCollectionRequestedEvent message)
    {
        var databaseKey = GetDatabaseKeyForSelectedNode();
        if (string.IsNullOrEmpty(databaseKey))
        {
            SendStatus("No database selected");
            return;
        }

        try
        {
            var favorite = await _collectionService.GetFavoriteCollectionAsync(databaseKey);
            if (favorite == null)
            {
                SendStatus("No favorite collection set");
                return;
            }

            // Find the node in the tree and select it
            var node = FindCollectionNode(_collectionTreeViewModel.RootNodes, favorite.Id);
            if (node != null)
            {
                _collectionTreeViewModel.SelectedNode = node;
                SendStatus($"Switched to favorite: {favorite.Name}");
            }
            else
                SendStatus("Favorite collection not found in tree");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to switch to favorite collection");
            SendStatus($"Failed to switch to favorite: {ex.Message}", true);
        }
    }

    /// <summary>
    /// Handles SwitchToLastCollectionRequestedEvent to navigate to the previously selected collection.
    /// </summary>
    public void Receive(SwitchToLastCollectionRequestedEvent message)
    {
        if (_lastSelectedNode == null)
        {
            SendStatus("No previous collection to switch to");
            return;
        }

        _collectionTreeViewModel.SelectedNode = _lastSelectedNode;
        _logger.LogDebug("Switched to last collection: {NodeName}", _lastSelectedNode.Name);
    }

    /// <summary>
    /// Gets the database configuration key for the currently selected node.
    /// </summary>
    private string? GetDatabaseKeyForSelectedNode()
    {
        var node = _collectionTreeViewModel.SelectedNode;

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
    /// Recursively finds a collection node by ID in the tree.
    /// </summary>
    private static TreeNodeBase? FindCollectionNode(IEnumerable<TreeNodeBase> rootNodes, Guid collectionId)
    {
        foreach (var item in rootNodes)
        {
            if (item is DatabaseTreeNode dbNode)
            {
                var found = FindCollectionNodeRecursive(dbNode.Children, collectionId);
                if (found != null)
                    return found;
            }
            else
            {
                // Check if it's a collection node directly
                if (item is CollectionTreeNode collectionNode && collectionNode.Collection.Id == collectionId)
                    return item;

                var found = FindCollectionNodeRecursive(item.Children, collectionId);
                if (found != null)
                    return found;
            }
        }

        return null;
    }

    private static TreeNodeBase? FindCollectionNodeRecursive(IEnumerable<TreeNodeBase> nodes, Guid collectionId)
    {
        foreach (var item in nodes)
        {
            if (item is CollectionTreeNode collectionNode && collectionNode.Collection.Id == collectionId)
                return item;

            var found = FindCollectionNodeRecursive(item.Children, collectionId);
            if (found != null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// Sends a status update message to be displayed in the UI.
    /// </summary>
    private void SendStatus(string message, bool isError = false) => _messenger.Send(new StatusUpdateEvent(message, isError));
}
