using ClipMate.Core.Models.Networking;
using ClipMate.Core.ValueObjects;

namespace ClipMate.Core.Services;

/// <summary>
/// Keeps the latest Remote Search results of each Remote Database on the requesting Device, keyed by
/// (Device ID, Database ID): the counterpart of <see cref="SearchResultsCache" /> for Remote Databases. The Peer keeps
/// nothing about a search.
/// </summary>
public sealed class RemoteSearchResultsCache
{
    private readonly Dictionary<RemoteDatabaseAddress, RemoteSearchResult> _cache = [];
    private readonly Lock _lock = new();

    /// <summary>
    /// Stores the results of a Remote Search, replacing any earlier results for that Remote Database.
    /// </summary>
    public void SetResults(RemoteDatabaseAddress address, RemoteSearchResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        lock (_lock)
            _cache[address] = result;
    }

    /// <summary>
    /// Gets the latest Remote Search results for a Remote Database, or <c>null</c> if there are none.
    /// </summary>
    public RemoteSearchResult? GetResults(RemoteDatabaseAddress address)
    {
        lock (_lock)
            return _cache.GetValueOrDefault(address);
    }

    /// <summary>
    /// Forgets the Remote Search results for a Remote Database, e.g. when it is no longer shared or its Peer is unpaired.
    /// </summary>
    public void ClearResults(RemoteDatabaseAddress address)
    {
        lock (_lock)
            _cache.Remove(address);
    }
}
