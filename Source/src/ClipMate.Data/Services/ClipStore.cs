using ClipMate.Core.Models;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Platform.Helpers;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Creates every clip (ADR-0005): one content hash and checksum for all of them, and GLOSSARY's Duplicate rule for the
/// captures that ask for it.
/// </summary>
public sealed class ClipStore : IClipStore
{
    private readonly IDatabaseContextFactory _databaseContextFactory;
    private readonly ILogger<ClipStore> _logger;

    public ClipStore(IDatabaseContextFactory databaseContextFactory, ILogger<ClipStore> logger)
    {
        _databaseContextFactory = databaseContextFactory ?? throw new ArgumentNullException(nameof(databaseContextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<AddResult> AddAsync(string databaseKey, Clip clip, Guid? collectionId, Guid? folderId, bool rejectDuplicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(databaseKey);
        ArgumentNullException.ThrowIfNull(clip);

        clip.CollectionId = collectionId;
        clip.FolderId = folderId;

        // A copy made from stored rows has no content loaded and keeps the hash of the clip it was copied from
        if (HasContent(clip))
            clip.ContentHash = ContentHasher.HashClip(clip);

        clip.Checksum = ContentHasher.Checksum(clip.ContentHash);

        var clipRepository = _databaseContextFactory.GetClipRepository(databaseKey);
        if (rejectDuplicate && collectionId is { } id && await FindDuplicateAsync(databaseKey, clip, id, cancellationToken) is { } existing)
        {
            _logger.LogInformation("Not saving a duplicate clip: the same text as the newest clip {ClipId} in collection {CollectionId}",
                existing.Id, id);

            return new DuplicateFound(existing);
        }

        return new ClipAdded(await clipRepository.CreateAsync(clip, cancellationToken));
    }

    /// <summary>
    /// GLOSSARY's Duplicate: the same plain text as the newest clip, outside the Trash, in a collection that doesn't
    /// accept duplicates. A clip with no plain text is never a Duplicate.
    /// </summary>
    private async Task<Clip?> FindDuplicateAsync(string databaseKey, Clip clip, Guid collectionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(clip.TextContent) ||
            await _databaseContextFactory.GetCollectionRepository(databaseKey).GetByIdAsync(collectionId, cancellationToken) is not
                { AcceptDuplicates: false })
            return null;

        var clipRepository = _databaseContextFactory.GetClipRepository(databaseKey);

        return await clipRepository.GetNewestPlainTextAsync(collectionId, cancellationToken) is { } newest && newest.Text == clip.TextContent
            ? await clipRepository.GetByIdAsync(newest.ClipId, cancellationToken)
            : null;
    }

    private static bool HasContent(Clip clip) =>
        !string.IsNullOrEmpty(clip.TextContent) ||
        !string.IsNullOrEmpty(clip.RtfContent) ||
        !string.IsNullOrEmpty(clip.HtmlContent) ||
        clip.ImageData is { Length: > 0 } ||
        !string.IsNullOrEmpty(clip.FilePathsJson) ||
        clip.RawFormats.Count > 0;
}
