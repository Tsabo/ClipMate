using ClipMate.Core.Models;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services.Abstractions;

namespace ClipMate.App.Services;

/// <summary>
/// Describes a clip by its title and collection, looking up the collection's name (and, for a Remote Database, its
/// Peer's), for places that show a clip outside a ClipMate window such as the hotkey navigation popup.
/// </summary>
public sealed class ClipDescriber
{
    private readonly ICollectionService _collections;
    private readonly IRemoteDatabaseService _remoteDatabases;

    public ClipDescriber(ICollectionService collections, IRemoteDatabaseService remoteDatabases)
    {
        _collections = collections ?? throw new ArgumentNullException(nameof(collections));
        _remoteDatabases = remoteDatabases ?? throw new ArgumentNullException(nameof(remoteDatabases));
    }

    /// <summary>
    /// Describes <paramref name="clip" />, which is in the Database <paramref name="databaseKey" />.
    /// </summary>
    public async Task<ClipDescription> DescribeAsync(Clip clip, string? databaseKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clip);

        var collection = RemoteDatabaseKey.TryParse(databaseKey, out var address)
            ? await DescribeRemoteCollectionAsync(address, clip.CollectionId, cancellationToken)
            : await DescribeLocalCollectionAsync(databaseKey, clip.CollectionId, cancellationToken);

        return new ClipDescription(ClipDescription.TitleOf(clip), collection);
    }

    private async Task<string> DescribeLocalCollectionAsync(string? databaseKey, Guid? collectionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(databaseKey))
            return string.Empty;

        var collections = await _collections.GetAllByDatabaseKeyAsync(databaseKey, cancellationToken);
        return collections.FirstOrDefault(p => p.Id == collectionId)?.Title ?? string.Empty;
    }

    private async Task<string> DescribeRemoteCollectionAsync(RemoteDatabaseAddress address, Guid? collectionId, CancellationToken cancellationToken)
    {
        var database = _remoteDatabases.GetRemoteDatabases().FirstOrDefault(p => p.Address == address);
        string? collection = null;
        try
        {
            var tree = await _remoteDatabases.GetCollectionTreeAsync(address, cancellationToken);
            collection = tree.FirstOrDefault(p => p.Id == collectionId)?.Title;
        }
        catch (RemoteDatabaseUnavailableException)
        {
            // The Peer went offline: name the Remote Database instead of the collection
        }

        return ClipDescription.CollectionOf(collection ?? database?.Name ?? string.Empty, database?.PeerName);
    }
}
