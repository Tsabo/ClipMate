using ClipMate.Core.Models;
using ClipMate.Core.Services.Abstractions;

namespace ClipMate.App.Services;

/// <summary>
/// Describes a clip by its title and collection, looking up the collection's name (and, for a Remote Database, its
/// Peer's), for places that show a clip outside a ClipMate window such as the hotkey navigation popup.
/// </summary>
public sealed class ClipDescriber
{
    private readonly IDatabases _databases;

    public ClipDescriber(IDatabases databases)
    {
        _databases = databases ?? throw new ArgumentNullException(nameof(databases));
    }

    /// <summary>
    /// Describes <paramref name="clip" />, which is in the Database <paramref name="databaseKey" />.
    /// </summary>
    public async Task<ClipDescription> DescribeAsync(Clip clip, string? databaseKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clip);
        return new ClipDescription(ClipDescription.TitleOf(clip), await DescribeCollectionAsync(databaseKey, clip.CollectionId, cancellationToken));
    }

    private async Task<string> DescribeCollectionAsync(string? databaseKey, Guid? collectionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(databaseKey))
            return string.Empty;

        IDatabase database;
        try
        {
            database = _databases.Get(databaseKey);
        }
        catch (DatabaseUnavailableException)
        {
            return string.Empty;
        }

        string? collection = null;
        try
        {
            var tree = await database.GetTreeAsync(cancellationToken);
            collection = tree.FirstOrDefault(p => p.Id == collectionId)?.Title;
        }
        catch (DatabaseUnavailableException)
        {
            // The Peer went offline (or the file went missing): a Remote Database is named instead of the collection
        }

        // A Remote Database's clip is named with its Peer's
        return database.Remote is { } remote
            ? ClipDescription.CollectionOf(collection ?? database.Name, remote.PeerName)
            : ClipDescription.CollectionOf(collection ?? string.Empty);
    }
}
