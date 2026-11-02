using ClipMate.Core.Models;
using ClipMate.Core.Models.Capture;
using ClipMate.Core.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <inheritdoc cref="ICaptureTargetResolver" />
public sealed class CaptureTargetResolver : ICaptureTargetResolver
{
    private readonly ICollectionService _collectionService;
    private readonly IFolderService _folderService;
    private readonly ILogger<CaptureTargetResolver> _logger;

    public CaptureTargetResolver(ICollectionService collectionService, IFolderService folderService, ILogger<CaptureTargetResolver> logger)
    {
        _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
        _folderService = folderService ?? throw new ArgumentNullException(nameof(folderService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<CaptureTarget?> ResolveAsync(CancellationToken cancellationToken = default)
    {
        if (_collectionService.GetActiveDatabaseKey() is not { Length: > 0 } databaseKey)
            return null;

        var collection = await _collectionService.GetActiveAsync(cancellationToken);

        // The bounce, as in ClipMate 7: Read-only and virtual collections bounce like one that doesn't accept new clips
        if (!AcceptsCaptures(collection))
        {
            var bounced = await _collectionService.GetFirstAcceptingCollectionAsync(cancellationToken) ??
                          (await _collectionService.GetAllAsync(cancellationToken)).OrderBy(p => p.SortKey).First();

            _logger.LogInformation("Capture bounced from '{FromCollection}' to '{ToCollection}'", collection.Title, bounced.Title);
            collection = bounced;
        }

        return new CaptureTarget(databaseKey, collection, await GetFolderIdAsync(collection, cancellationToken));
    }

    private static bool AcceptsCaptures(Collection collection) =>
        collection is { AcceptNewClips: true, ReadOnly: false, IsVirtual: false };

    /// <summary>
    /// The active folder when it belongs to the target collection and takes captures, otherwise the collection's Inbox.
    /// </summary>
    private async Task<Guid?> GetFolderIdAsync(Collection collection, CancellationToken cancellationToken)
    {
        var activeFolder = await _folderService.GetActiveAsync(cancellationToken);
        if (activeFolder is { FolderType: not FolderType.SearchResults } && activeFolder.CollectionId == collection.Id)
            return activeFolder.Id;

        var rootFolders = await _folderService.GetRootFoldersAsync(collection.Id, cancellationToken);

        return rootFolders.FirstOrDefault(p => p.FolderType == FolderType.Inbox)?.Id;
    }
}
