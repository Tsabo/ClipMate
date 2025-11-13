using ClipMate.Core.Models;
using ClipMate.Core.Services;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Coordinates clipboard monitoring and clip persistence.
/// Wires ClipboardService events to ClipService for automatic saving.
/// Includes application filtering to exclude clips from specific applications.
/// </summary>
public class ClipboardCoordinator : IDisposable
{
    private readonly IClipboardService _clipboardService;
    private readonly IClipService _clipService;
    private readonly ICollectionService _collectionService;
    private readonly IFolderService _folderService;
    private readonly IApplicationFilterService _filterService;
    private readonly ILogger<ClipboardCoordinator> _logger;
    private bool _disposed;

    public ClipboardCoordinator(
        IClipboardService clipboardService,
        IClipService clipService,
        ICollectionService collectionService,
        IFolderService folderService,
        IApplicationFilterService filterService,
        ILogger<ClipboardCoordinator> logger)
    {
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        _clipService = clipService ?? throw new ArgumentNullException(nameof(clipService));
        _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
        _folderService = folderService ?? throw new ArgumentNullException(nameof(folderService));
        _filterService = filterService ?? throw new ArgumentNullException(nameof(filterService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Subscribe to clipboard capture events
        _clipboardService.ClipCaptured += OnClipCaptured;
    }

    /// <summary>
    /// Starts clipboard monitoring and automatic clip saving.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting clipboard coordinator");
        await _clipboardService.StartMonitoringAsync(cancellationToken);
    }

    /// <summary>
    /// Stops clipboard monitoring.
    /// </summary>
    public async Task StopAsync()
    {
        _logger.LogInformation("Stopping clipboard coordinator");
        await _clipboardService.StopMonitoringAsync();
    }

    private async void OnClipCaptured(object? sender, ClipCapturedEventArgs e)
    {
        if (e.Cancel)
        {
            _logger.LogDebug("Clip capture cancelled by event handler");
            return;
        }

        try
        {
            // Check if clip should be filtered based on source application
            var shouldFilter = await _filterService.ShouldFilterAsync(
                e.Clip.SourceApplicationName,
                e.Clip.SourceApplicationTitle);

            if (shouldFilter)
            {
                _logger.LogDebug(
                    "Clip filtered out: Process={ProcessName}, Title={WindowTitle}",
                    e.Clip.SourceApplicationName,
                    e.Clip.SourceApplicationTitle);
                return;
            }

            _logger.LogDebug(
                "Saving clip: Type={ClipType}, Hash={ContentHash}, Length={Length}",
                e.Clip.Type,
                e.Clip.ContentHash,
                e.Clip.TextContent?.Length ?? 0);

            // Assign clip to the active collection and folder
            try
            {
                var activeCollection = await _collectionService.GetActiveAsync();
                e.Clip.CollectionId = activeCollection.Id;
                
                // Get the active folder (or default to Inbox folder)
                var activeFolder = await _folderService.GetActiveAsync();
                if (activeFolder != null)
                {
                    // Check if folder accepts clipboard captures
                    if (activeFolder.FolderType == FolderType.SearchResults)
                    {
                        _logger.LogWarning("Active folder is SearchResults (read-only), falling back to Inbox");
                        activeFolder = null; // Fall through to Inbox
                    }
                }
                
                if (activeFolder != null)
                {
                    e.Clip.FolderId = activeFolder.Id;
                    _logger.LogDebug("Assigning clip to folder: CollectionId={CollectionId}, FolderId={FolderId}, FolderName={FolderName}, FolderType={FolderType}",
                        activeCollection.Id, activeFolder.Id, activeFolder.Name, activeFolder.FolderType);
                }
                else
                {
                    // Fallback: Find the Inbox folder in the active collection
                    var rootFolders = await _folderService.GetRootFoldersAsync(activeCollection.Id);
                    var inboxFolder = rootFolders.FirstOrDefault(f => f.FolderType == FolderType.Inbox);
                    if (inboxFolder != null)
                    {
                        e.Clip.FolderId = inboxFolder.Id;
                        _logger.LogDebug("No active folder, using Inbox: CollectionId={CollectionId}, FolderId={FolderId}",
                            activeCollection.Id, inboxFolder.Id);
                    }
                    else
                    {
                        _logger.LogWarning("No active folder and no Inbox folder found, clip will not be assigned to a folder");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to assign clip to collection/folder, clip will be saved without assignment");
                // Continue saving the clip even if we can't get active collection/folder
            }

            // ClipService handles duplicate detection via content hash
            var savedClip = await _clipService.CreateAsync(e.Clip);

            if (savedClip.Id == e.Clip.Id)
            {
                _logger.LogInformation("Clip saved successfully: {ClipId}", savedClip.Id);
            }
            else
            {
                _logger.LogDebug(
                    "Duplicate clip detected, using existing: {ExistingId}",
                    savedClip.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to save clip: Type={ClipType}, Hash={ContentHash}",
                e.Clip.Type,
                e.Clip.ContentHash);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _clipboardService.ClipCaptured -= OnClipCaptured;
        _disposed = true;
    }
}
