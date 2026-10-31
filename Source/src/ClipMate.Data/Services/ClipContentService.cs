using ClipMate.Core.Models;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services.Abstractions;

namespace ClipMate.Data.Services;

/// <summary>
/// <see cref="IClipContentService" /> routed by database key: a local Database is read through its repositories, a
/// Remote Database through <see cref="IRemoteDatabaseService" /> (its Peer). Never reads the decrypted-content cache,
/// so encrypted clips always come back as stored.
/// </summary>
internal sealed class ClipContentService : IClipContentService
{
    private readonly IDatabaseContextFactory _databaseContextFactory;
    private readonly IRemoteDatabaseService _remoteDatabases;

    public ClipContentService(IDatabaseContextFactory databaseContextFactory, IRemoteDatabaseService remoteDatabases)
    {
        _databaseContextFactory = databaseContextFactory ?? throw new ArgumentNullException(nameof(databaseContextFactory));
        _remoteDatabases = remoteDatabases ?? throw new ArgumentNullException(nameof(remoteDatabases));
    }

    /// <inheritdoc />
    public async Task<ClipContent?> GetClipContentAsync(string databaseKey, Guid clipId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseKey);

        if (RemoteDatabaseKey.TryParse(databaseKey, out var address))
            return await _remoteDatabases.GetClipContentAsync(address, clipId, cancellationToken);

        var clip = await _databaseContextFactory.GetClipRepository(databaseKey).GetByIdAsync(clipId, cancellationToken);
        if (clip == null)
            return null;

        var formats = await _databaseContextFactory.GetClipDataRepository(databaseKey).GetByClipIdAsync(clipId, cancellationToken);
        var blobRepository = _databaseContextFactory.GetBlobRepository(databaseKey);
        var textBlobs = await blobRepository.GetTextByClipIdAsync(clipId, cancellationToken);
        var jpgBlobs = await blobRepository.GetJpgByClipIdAsync(clipId, cancellationToken);
        var pngBlobs = await blobRepository.GetPngByClipIdAsync(clipId, cancellationToken);
        var binaryBlobs = await blobRepository.GetBlobByClipIdAsync(clipId, cancellationToken);

        return new ClipContent(clip, formats, textBlobs, jpgBlobs, pngBlobs, binaryBlobs);
    }
}
