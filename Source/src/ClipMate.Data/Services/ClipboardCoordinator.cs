using System.Windows;
using ClipMate.Core.Events;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Background service that coordinates clipboard monitoring and clip persistence.
/// Consumes clips from the clipboard service channel and saves them to the database.
/// </summary>
public class ClipboardCoordinator : IHostedService
{
    private readonly IClipboardService _clipboardService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IMessenger _messenger;
    private readonly ILogger<ClipboardCoordinator> _logger;
    private Task? _processingTask;
    private CancellationTokenSource? _cts;

    public ClipboardCoordinator(
        IClipboardService clipboardService,
        IServiceProvider serviceProvider,
        IMessenger messenger,
        ILogger<ClipboardCoordinator> logger)
    {
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting clipboard coordinator");

        try
        {
            // Start clipboard monitoring
            // Check if we have an Application dispatcher (UI thread), otherwise start directly
            if (Application.Current?.Dispatcher != null)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _clipboardService.StartMonitoringAsync(cancellationToken);
                });
            }
            else
            {
                // No dispatcher available (e.g., in tests), start directly
                await _clipboardService.StartMonitoringAsync(cancellationToken);
            }

            // Start background task to process clips from channel
            _cts = new CancellationTokenSource();
            _processingTask = ProcessClipsAsync(_cts.Token);

            _logger.LogInformation("Clipboard coordinator started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start clipboard coordinator");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping clipboard coordinator");

        try
        {
            // Stop clipboard monitoring (completes the channel)
            await _clipboardService.StopMonitoringAsync();

            // Cancel processing and wait for it to finish
            _cts?.Cancel();

            if (_processingTask != null)
            {
                await _processingTask;
            }

            _cts?.Dispose();
            _logger.LogInformation("Clipboard coordinator stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping clipboard coordinator");
        }
    }

    /// <summary>
    /// Background task that consumes clips from the channel and persists them.
    /// Runs until the channel is completed or cancellation is requested.
    /// </summary>
    private async Task ProcessClipsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Clip processing loop started");

        try
        {
            // Read all clips from the channel until it's completed
            await foreach (var clip in _clipboardService.ClipsChannel.ReadAllAsync(cancellationToken))
            {
                try
                {
                    await ProcessClipAsync(clip, cancellationToken);
                }
                catch (Exception ex)
                {
                    // Log error but continue processing other clips
                    _logger.LogError(ex,
                        "Failed to process clip: Type={ClipType}, Hash={ContentHash}",
                        clip.Type,
                        clip.ContentHash);
                }
            }

            _logger.LogInformation("Clip processing loop completed (channel closed)");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Clip processing loop cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in clip processing loop");
        }
    }

    /// <summary>
    /// Processes a single clip: applies filters, assigns to collection/folder, and persists.
    /// </summary>
    private async Task ProcessClipAsync(Core.Models.Clip clip, CancellationToken cancellationToken)
    {
        // Create a scope to resolve scoped services
        using var scope = _serviceProvider.CreateScope();
        var clipService = scope.ServiceProvider.GetRequiredService<IClipService>();
        var collectionService = scope.ServiceProvider.GetRequiredService<ICollectionService>();
        var folderService = scope.ServiceProvider.GetRequiredService<IFolderService>();
        var filterService = scope.ServiceProvider.GetRequiredService<IApplicationFilterService>();

        // Check if clip should be filtered based on source application
        var shouldFilter = await filterService.ShouldFilterAsync(
            clip.SourceApplicationName,
            clip.SourceApplicationTitle,
            cancellationToken);

        if (shouldFilter)
        {
            _logger.LogDebug(
                "Clip filtered out: Process={ProcessName}, Title={WindowTitle}",
                clip.SourceApplicationName,
                clip.SourceApplicationTitle);
            return;
        }

        _logger.LogDebug(
            "Processing clip: Type={ClipType}, Hash={ContentHash}, Length={Length}",
            clip.Type,
            clip.ContentHash,
            clip.TextContent?.Length ?? 0);

        // Assign clip to the active collection and folder
        try
        {
            var activeCollection = await collectionService.GetActiveAsync(cancellationToken);
            clip.CollectionId = activeCollection.Id;

            // Get the active folder (or default to Inbox folder)
            var activeFolder = await folderService.GetActiveAsync(cancellationToken);
            if (activeFolder != null)
            {
                // Check if folder accepts clipboard captures
                if (activeFolder.FolderType == Core.Models.FolderType.SearchResults)
                {
                    _logger.LogWarning("Active folder is SearchResults (read-only), falling back to Inbox");
                    activeFolder = null; // Fall through to Inbox
                }
            }

            if (activeFolder != null)
            {
                clip.FolderId = activeFolder.Id;
                _logger.LogDebug("Assigning clip to folder: CollectionId={CollectionId}, FolderId={FolderId}, FolderName={FolderName}, FolderType={FolderType}",
                    activeCollection.Id, activeFolder.Id, activeFolder.Name, activeFolder.FolderType);
            }
            else
            {
                // Fallback: Find the Inbox folder in the active collection
                var rootFolders = await folderService.GetRootFoldersAsync(activeCollection.Id, cancellationToken);
                var inboxFolder = rootFolders.FirstOrDefault(f => f.FolderType == Core.Models.FolderType.Inbox);
                if (inboxFolder != null)
                {
                    clip.FolderId = inboxFolder.Id;
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
        var savedClip = await clipService.CreateAsync(clip, cancellationToken);

        var wasDuplicate = savedClip.Id != clip.Id;

        if (!wasDuplicate)
        {
            _logger.LogInformation("Clip saved successfully: {ClipId}", savedClip.Id);
        }
        else
        {
            _logger.LogDebug(
                "Duplicate clip detected, using existing: {ExistingId}",
                savedClip.Id);
        }

        // Send message to notify UI - always send, even for duplicates
        // UI can decide how to handle duplicates (update timestamp, bring to top, etc.)
        var clipAddedEvent = new ClipAddedEvent(
            savedClip,
            wasDuplicate,
            savedClip.CollectionId,
            savedClip.FolderId);

        _messenger.Send(clipAddedEvent);
    }
}
