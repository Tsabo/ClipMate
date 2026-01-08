using System.Diagnostics;
using ClipMate.App.Models.TreeNodes;
using ClipMate.App.ViewModels;
using ClipMate.App.Views.Dialogs;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using DevExpress.Xpf.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shortcut = ClipMate.Core.Models.Shortcut;

namespace ClipMate.App.Services;

/// <summary>
/// Coordinates clip operations (delete, rename, copy, move, export) across all windows.
/// Centralizes event handling that was previously duplicated in ExplorerWindowViewModel and ClassicViewModel.
/// Sends StatusUpdateEvent for status bar updates that ViewModels can handle.
/// </summary>
public class ClipOperationsCoordinator :
    IRecipient<DeleteClipsRequestedEvent>,
    IRecipient<RenameClipRequestedEvent>,
    IRecipient<CopyToCollectionRequestedEvent>,
    IRecipient<MoveToCollectionRequestedEvent>,
    IRecipient<CreateNewClipRequestedEvent>,
    IRecipient<ExportToXmlRequestedEvent>,
    IRecipient<ExportToFilesRequestedEvent>,
    IRecipient<ShowSearchWindowEvent>,
    IRecipient<PowerPasteUpRequestedEvent>,
    IRecipient<PowerPasteDownRequestedEvent>,
    IRecipient<PowerPasteToggleRequestedEvent>,
    IRecipient<OpenSourceUrlRequestedEvent>,
    IRecipient<CleanUpTextRequestedEvent>,
    IRecipient<RemoveLineBreaksRequestedEvent>,
    IRecipient<StripNonTextRequestedEvent>,
    IRecipient<CaseConversionRequestedEvent>,
    IRecipient<ShowClipPropertiesRequestedEvent>
{
    private readonly IActiveWindowService _activeWindowService;
    private readonly ClipListViewModel _clipListViewModel;
    private readonly IClipService _clipService;
    private readonly ICollectionService _collectionService;
    private readonly CollectionTreeViewModel _collectionTreeViewModel;
    private readonly ILogger<ClipOperationsCoordinator> _logger;
    private readonly IMessenger _messenger;
    private readonly IPowerPasteService _powerPasteService;
    private readonly ISearchService _searchService;
    private readonly IServiceProvider _serviceProvider;

    public ClipOperationsCoordinator(IActiveWindowService activeWindowService,
        ClipListViewModel clipListViewModel,
        CollectionTreeViewModel collectionTreeViewModel,
        IClipService clipService,
        ICollectionService collectionService,
        IPowerPasteService powerPasteService,
        ISearchService searchService,
        IMessenger messenger,
        IServiceProvider serviceProvider,
        ILogger<ClipOperationsCoordinator> logger)
    {
        _activeWindowService = activeWindowService ?? throw new ArgumentNullException(nameof(activeWindowService));
        _clipListViewModel = clipListViewModel ?? throw new ArgumentNullException(nameof(clipListViewModel));
        _collectionTreeViewModel = collectionTreeViewModel ?? throw new ArgumentNullException(nameof(collectionTreeViewModel));
        _clipService = clipService ?? throw new ArgumentNullException(nameof(clipService));
        _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
        _powerPasteService = powerPasteService ?? throw new ArgumentNullException(nameof(powerPasteService));
        _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Register for all clip operation events
        _messenger.Register<DeleteClipsRequestedEvent>(this);
        _messenger.Register<RenameClipRequestedEvent>(this);
        _messenger.Register<CopyToCollectionRequestedEvent>(this);
        _messenger.Register<MoveToCollectionRequestedEvent>(this);
        _messenger.Register<CreateNewClipRequestedEvent>(this);
        _messenger.Register<ExportToXmlRequestedEvent>(this);
        _messenger.Register<ExportToFilesRequestedEvent>(this);
        _messenger.Register<ShowSearchWindowEvent>(this);
        _messenger.Register<PowerPasteUpRequestedEvent>(this);
        _messenger.Register<PowerPasteDownRequestedEvent>(this);
        _messenger.Register<PowerPasteToggleRequestedEvent>(this);
        _messenger.Register<OpenSourceUrlRequestedEvent>(this);
        _messenger.Register<CleanUpTextRequestedEvent>(this);
        _messenger.Register<RemoveLineBreaksRequestedEvent>(this);
        _messenger.Register<StripNonTextRequestedEvent>(this);
        _messenger.Register<CaseConversionRequestedEvent>(this);
        _messenger.Register<ShowClipPropertiesRequestedEvent>(this);

        _logger.LogDebug("ClipOperationsCoordinator initialized and registered for events");
    }

    /// <summary>
    /// Handles ShowClipPropertiesRequestedEvent to show the clip properties dialog.
    /// </summary>
    public async void Receive(ShowClipPropertiesRequestedEvent message)
    {
        var selectedClip = _clipListViewModel.SelectedClip;

        if (selectedClip == null)
        {
            _logger.LogDebug("ShowClipProperties: No clip selected");
            return;
        }

        try
        {
            var dialog = new ClipPropertiesDialog();
            var viewModel = _serviceProvider.GetRequiredService<ClipPropertiesViewModel>();

            await viewModel.LoadClipAsync(selectedClip);
            dialog.DataContext = viewModel;
            dialog.Owner = _activeWindowService.DialogOwner;
            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show clip properties for clip {ClipId}", selectedClip.Id);
            SendStatus($"Failed to show clip properties: {ex.Message}", true);
        }
    }

    /// <summary>
    /// Handles CaseConversionRequestedEvent to convert text case in selected clip.
    /// </summary>
    public void Receive(CaseConversionRequestedEvent message)
    {
        var selectedClip = _clipListViewModel.SelectedClip;

        if (selectedClip == null)
        {
            SendStatus("No clip selected");
            return;
        }

        // TODO: Implement case conversion (requires ClipData modification)
        var typeName = message.ConversionType.ToString();
        SendStatus($"Case Conversion ({typeName}): Feature coming in a future update");
    }

    /// <summary>
    /// Handles CleanUpTextRequestedEvent to clean up whitespace in selected clip.
    /// </summary>
    public void Receive(CleanUpTextRequestedEvent message)
    {
        var selectedClip = _clipListViewModel.SelectedClip;

        if (selectedClip == null)
        {
            SendStatus("No clip selected");
            return;
        }

        // TODO: Implement text cleanup (requires ClipData modification)
        // This would normalize line endings, trim trailing whitespace, collapse multiple blank lines
        SendStatus("Clean-Up Text: Feature coming in a future update");
    }

    /// <summary>
    /// Handles CopyToCollectionRequestedEvent to copy clips to another collection.
    /// </summary>
    public async void Receive(CopyToCollectionRequestedEvent message)
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
            _logger.LogError("Cannot copy clips: source database key not found");
            SendStatus("Error: source database not found", true);
            return;
        }

        // Create and show collection picker dialog
        var dialog = new CollectionPickerDialog(_serviceProvider)
        {
            Message = $"Select a collection to copy {selectedClips.Count} clip(s) to:",
            Owner = _activeWindowService.DialogOwner,
        };

        await dialog.LoadCollectionsAsync();

        if (dialog.ShowDialog() != true || dialog.SelectedCollectionId is null || string.IsNullOrEmpty(dialog.SelectedDatabaseKey))
        {
            SendStatus("Copy cancelled");
            return;
        }

        try
        {
            var targetDatabaseKey = dialog.SelectedDatabaseKey;
            var isCrossDatabase = !sourceDatabaseKey.Equals(targetDatabaseKey, StringComparison.OrdinalIgnoreCase);

            var copiedCount = 0;

            foreach (var item in selectedClips)
            {
                if (isCrossDatabase)
                {
                    await _clipService.CopyClipCrossDatabaseAsync(
                        sourceDatabaseKey,
                        item.Id,
                        targetDatabaseKey,
                        dialog.SelectedCollectionId.Value);
                }
                else
                {
                    await _clipService.CopyClipAsync(
                        sourceDatabaseKey,
                        item.Id,
                        dialog.SelectedCollectionId.Value);
                }

                copiedCount++;
            }

            var databaseMessage = isCrossDatabase
                ? " (cross-database)"
                : string.Empty;

            SendStatus($"Copied {copiedCount} clip(s){databaseMessage}");

            // Request clip list reload
            _messenger.Send(new ReloadClipsRequestedEvent());

            _logger.LogInformation("Copied {Count} clip(s) to collection {CollectionId}", copiedCount, dialog.SelectedCollectionId.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy clips to collection");
            SendStatus("Error copying clips", true);
        }
    }

    /// <summary>
    /// Handles CreateNewClipRequestedEvent to create a new empty clip.
    /// </summary>
    public async void Receive(CreateNewClipRequestedEvent message)
    {
        var currentCollectionId = _clipListViewModel.CurrentCollectionId;

        if (currentCollectionId == null)
        {
            SendStatus("No collection selected");
            return;
        }

        var databaseKey = GetDatabaseKeyForSelectedNode();

        if (string.IsNullOrEmpty(databaseKey))
        {
            _logger.LogError("Cannot create clip: database key not found");
            SendStatus("Error: database not found", true);
            return;
        }

        try
        {
            var newClip = new Clip
            {
                Title = "New Clip",
                TextContent = string.Empty,
                CollectionId = currentCollectionId.Value,
                CapturedAt = DateTimeOffset.UtcNow,
                Type = ClipType.Text,
            };

            var createdClip = await _clipService.CreateAsync(databaseKey, newClip);

            SendStatus($"Created new clip: {createdClip.Title}");

            // Request clip list reload
            _messenger.Send(new ReloadClipsRequestedEvent());

            _logger.LogInformation("Created new clip {ClipId}", createdClip.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create new clip");
            SendStatus("Error creating new clip", true);
        }
    }

    /// <summary>
    /// Handles DeleteClipsRequestedEvent to delete selected clips with confirmation.
    /// </summary>
    public async void Receive(DeleteClipsRequestedEvent message)
    {
        var selectedClips = _clipListViewModel.SelectedClips;

        if (selectedClips.Count == 0)
        {
            SendStatus("No clips selected");
            return;
        }

        var clipCount = selectedClips.Count;
        var confirmMessage = clipCount == 1
            ? $"Delete '{selectedClips[0].Title}'?"
            : $"Delete {clipCount} clips?";

        var result = DXMessageBox.Show(
            confirmMessage,
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            var databaseKey = GetDatabaseKeyForSelectedNode();

            if (string.IsNullOrEmpty(databaseKey))
            {
                _logger.LogError("Cannot delete clips: database key not found");
                SendStatus("Error: database not found", true);
                return;
            }

            foreach (var item in selectedClips)
                await _clipService.DeleteAsync(databaseKey, item.Id);

            SendStatus($"Deleted {clipCount} clip(s)");

            // Request clip list reload
            _messenger.Send(new ReloadClipsRequestedEvent());

            _logger.LogInformation("Deleted {Count} clip(s)", clipCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete clips");
            SendStatus("Error deleting clips", true);
        }
    }

    /// <summary>
    /// Handles ExportToFilesRequestedEvent to display the flat-file export dialog.
    /// </summary>
    public void Receive(ExportToFilesRequestedEvent message)
    {
        var selectedClips = _clipListViewModel.SelectedClips;

        if (selectedClips.Count == 0)
        {
            DXMessageBox.Show(
                "Please select at least one clip to export.",
                "No Selection",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        var vm = ActivatorUtilities.CreateInstance<FlatFileExportViewModel>(_serviceProvider);
        vm.Initialize(selectedClips);

        var dialog = new FlatFileExportDialog(vm)
        {
            Owner = _activeWindowService.DialogOwner,
        };

        dialog.ShowDialog();
    }

    /// <summary>
    /// Handles ExportToXmlRequestedEvent to display the XML export dialog.
    /// </summary>
    public async void Receive(ExportToXmlRequestedEvent message)
    {
        var selectedClips = _clipListViewModel.SelectedClips;

        // Get current collection info
        var collectionName = "Clips";
        Guid? collectionId = null;

        if (_collectionTreeViewModel.SelectedNode is CollectionTreeNode collectionNode)
        {
            collectionName = collectionNode.Collection.Title;
            collectionId = collectionNode.Collection.Id;
        }

        var vm = ActivatorUtilities.CreateInstance<XmlExportViewModel>(_serviceProvider);

        // Check if only one clip is selected - offer to export entire collection
        if (selectedClips.Count == 1 && collectionId != null)
        {
            var result = DXMessageBox.Show(
                $"You have selected only one clip. Would you like to select the whole collection [{collectionName}] instead?",
                "Confirm Action",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Initialize with empty list first, then load entire collection
                vm.Initialize([], collectionName, collectionId);
                await vm.LoadEntireCollectionAsync();
            }
            else
                vm.Initialize(selectedClips, collectionName, collectionId);
        }
        else
            vm.Initialize(selectedClips, collectionName, collectionId);

        var dialog = new XmlExportDialog(vm)
        {
            Owner = _activeWindowService.DialogOwner,
        };

        dialog.ShowDialog();
    }

    /// <summary>
    /// Handles MoveToCollectionRequestedEvent to move clips to another collection.
    /// </summary>
    public async void Receive(MoveToCollectionRequestedEvent message)
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
            _logger.LogError("Cannot move clips: source database key not found");
            SendStatus("Error: source database not found", true);
            return;
        }

        // Create and show collection picker dialog
        var dialog = new CollectionPickerDialog(_serviceProvider)
        {
            Message = $"Select a collection to move {selectedClips.Count} clip(s) to:",
            Owner = _activeWindowService.DialogOwner,
        };

        await dialog.LoadCollectionsAsync();

        if (dialog.ShowDialog() != true || dialog.SelectedCollectionId is null || string.IsNullOrEmpty(dialog.SelectedDatabaseKey))
        {
            SendStatus("Move cancelled");
            return;
        }

        try
        {
            var targetDatabaseKey = dialog.SelectedDatabaseKey;
            var isCrossDatabase = !sourceDatabaseKey.Equals(targetDatabaseKey, StringComparison.OrdinalIgnoreCase);

            var movedCount = 0;

            foreach (var item in selectedClips)
            {
                if (isCrossDatabase)
                {
                    await _clipService.MoveClipCrossDatabaseAsync(
                        sourceDatabaseKey,
                        item.Id,
                        targetDatabaseKey,
                        dialog.SelectedCollectionId.Value);
                }
                else
                {
                    await _clipService.MoveClipAsync(
                        sourceDatabaseKey,
                        item.Id,
                        dialog.SelectedCollectionId.Value);
                }

                movedCount++;
            }

            var databaseMessage = isCrossDatabase
                ? " (cross-database)"
                : string.Empty;

            SendStatus($"Moved {movedCount} clip(s){databaseMessage}");

            // Request clip list reload
            _messenger.Send(new ReloadClipsRequestedEvent());

            _logger.LogInformation("Moved {Count} clip(s) to collection {CollectionId}", movedCount, dialog.SelectedCollectionId.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move clips to collection");
            SendStatus("Error moving clips", true);
        }
    }

    /// <summary>
    /// Handles OpenSourceUrlRequestedEvent to open the source URL in the default browser.
    /// </summary>
    public void Receive(OpenSourceUrlRequestedEvent message)
    {
        var selectedClip = _clipListViewModel.SelectedClip;

        if (selectedClip == null)
        {
            SendStatus("No clip selected");
            return;
        }

        if (string.IsNullOrEmpty(selectedClip.SourceUrl))
        {
            SendStatus("Selected clip has no source URL");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = selectedClip.SourceUrl,
                UseShellExecute = true,
            });

            _logger.LogInformation("Opened source URL: {Url}", selectedClip.SourceUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open source URL: {Url}", selectedClip.SourceUrl);
            SendStatus($"Failed to open URL: {ex.Message}", true);
        }
    }

    /// <summary>
    /// Handles PowerPasteDownRequestedEvent to start PowerPaste in downward direction.
    /// </summary>
    public async void Receive(PowerPasteDownRequestedEvent message)
    {
        var selectedClips = _clipListViewModel.SelectedClips;

        if (selectedClips.Count == 0)
        {
            SendStatus("No clips selected for PowerPaste");
            return;
        }

        try
        {
            await _powerPasteService.StartAsync(selectedClips, PowerPasteDirection.Down);
            SendStatus($"PowerPaste Down started with {selectedClips.Count} clip(s)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start PowerPaste Down");
            SendStatus("Error starting PowerPaste", true);
        }
    }

    /// <summary>
    /// Handles PowerPasteToggleRequestedEvent to toggle PowerPaste state.
    /// </summary>
    public async void Receive(PowerPasteToggleRequestedEvent message)
    {
        // If PowerPaste is active, stop it
        if (_powerPasteService.State == PowerPasteState.Active)
        {
            _powerPasteService.Stop();
            SendStatus("PowerPaste stopped");
            return;
        }

        // Otherwise, start it in the default direction (Down)
        var selectedClips = _clipListViewModel.SelectedClips;

        if (selectedClips.Count == 0)
        {
            SendStatus("No clips selected for PowerPaste");
            return;
        }

        try
        {
            await _powerPasteService.StartAsync(selectedClips, PowerPasteDirection.Down);
            SendStatus($"PowerPaste started with {selectedClips.Count} clip(s)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle PowerPaste");
            SendStatus("Error starting PowerPaste", true);
        }
    }

    /// <summary>
    /// Handles PowerPasteUpRequestedEvent to start PowerPaste in upward direction.
    /// </summary>
    public async void Receive(PowerPasteUpRequestedEvent message)
    {
        var selectedClips = _clipListViewModel.SelectedClips;

        if (selectedClips.Count == 0)
        {
            SendStatus("No clips selected for PowerPaste");
            return;
        }

        try
        {
            await _powerPasteService.StartAsync(selectedClips, PowerPasteDirection.Up);
            SendStatus($"PowerPaste Up started with {selectedClips.Count} clip(s)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start PowerPaste Up");
            SendStatus("Error starting PowerPaste", true);
        }
    }

    /// <summary>
    /// Handles RemoveLineBreaksRequestedEvent to remove line breaks from selected clip.
    /// </summary>
    public void Receive(RemoveLineBreaksRequestedEvent message)
    {
        var selectedClip = _clipListViewModel.SelectedClip;

        if (selectedClip == null)
        {
            SendStatus("No clip selected");
            return;
        }

        // TODO: Implement line break removal (requires ClipData modification)
        SendStatus("Remove Linebreaks: Feature coming in a future update");
    }

    /// <summary>
    /// Handles RenameClipRequestedEvent to rename a clip with a dialog.
    /// </summary>
    public async void Receive(RenameClipRequestedEvent message)
    {
        var selectedClip = _clipListViewModel.SelectedClip;

        if (selectedClip == null)
        {
            SendStatus("No clip selected");
            return;
        }

        var databaseKey = GetDatabaseKeyForSelectedNode();

        if (string.IsNullOrEmpty(databaseKey))
        {
            _logger.LogError("Cannot rename clip: database key not found");
            SendStatus("Error: database not found", true);
            return;
        }

        try
        {
            // Get the RenameClipDialogViewModel from DI
            var viewModel = _serviceProvider.GetService<RenameClipDialogViewModel>();

            if (viewModel == null)
            {
                _logger.LogError("RenameClipDialogViewModel not found in DI container");
                SendStatus("Error: dialog service not available", true);
                return;
            }

            // Get existing shortcut if any
            var shortcutService = _serviceProvider.GetService<IShortcutService>();
            Shortcut? existingShortcut = null;

            if (shortcutService != null)
            {
                try
                {
                    existingShortcut = await shortcutService.GetByClipIdAsync(databaseKey, selectedClip.Id);
                }
                catch (Exception ex) when (ex.Message.Contains("no such table"))
                {
                    // ShortCut table doesn't exist yet - this is OK
                    _logger.LogDebug("ShortCut table not found - will be created on first shortcut save");
                }
            }

            // Initialize the dialog ViewModel
            await viewModel.InitializeAsync(
                selectedClip.Id,
                databaseKey,
                selectedClip.Title,
                existingShortcut?.Nickname);

            // Create and show the dialog
            var dialog = new RenameClipDialog
            {
                DataContext = viewModel,
                Owner = _activeWindowService.DialogOwner,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };

            if (dialog.ShowDialog() == true)
            {
                // Update the clip in the collection
                var clip = _clipListViewModel.Clips.FirstOrDefault(p => p.Id == selectedClip.Id);

                clip?.Title = viewModel.Title;

                var title = viewModel.Title ?? string.Empty;
                SendStatus($"Updated clip: {title}");

                _logger.LogInformation("Renamed clip {ClipId} to '{Title}'", selectedClip.Id, viewModel.Title);
            }
            else
                SendStatus("Rename cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show rename dialog");
            SendStatus("Error showing rename dialog", true);
        }
    }

    /// <summary>
    /// Handles ShowSearchWindowEvent to display the search dialog.
    /// </summary>
    public void Receive(ShowSearchWindowEvent message)
    {
        _logger.LogInformation("Showing search window");

        try
        {
            var searchViewModel = _serviceProvider.GetRequiredService<SearchViewModel>();
            var logger = _serviceProvider.GetRequiredService<ILogger<SearchDialog>>();
            var dialog = new SearchDialog(searchViewModel, _searchService, _collectionService, logger)
            {
                Owner = _activeWindowService.DialogOwner,
            };

            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show search window");
            SendStatus("Error showing search window", true);
        }
    }

    /// <summary>
    /// Handles StripNonTextRequestedEvent to remove non-text formats from selected clip.
    /// </summary>
    public void Receive(StripNonTextRequestedEvent message)
    {
        var selectedClip = _clipListViewModel.SelectedClip;

        if (selectedClip == null)
        {
            SendStatus("No clip selected");
            return;
        }

        // TODO: Implement non-text stripping (requires deleting ClipData entries and blobs)
        SendStatus("Strip Non-Text: Feature coming in a future update");
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
    /// Sends a status update message to be displayed in the UI.
    /// </summary>
    /// <param name="message">The status message.</param>
    /// <param name="isError">Whether this is an error message.</param>
    private void SendStatus(string message, bool isError = false) => _messenger.Send(new StatusUpdateEvent(message, isError));
}
