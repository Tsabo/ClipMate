using ClipMate.Core.Models;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Models.Search;

namespace ClipMate.Core.Services.Abstractions;

/// <summary>
/// The requesting side of Shared Databases: the Remote Databases this Device can browse, one for each Shared Database
/// of each Peer, and read access to them (a read-only live proxy; no SQL crosses the wire).
/// <para>
/// Remote Databases are built at runtime from the Peer records and Peer status, never from database configuration.
/// A Remote Database whose Peer is offline stays listed (as unavailable) until the Peer is unpaired or stops sharing it.
/// While networking is off there are none.
/// </para>
/// </summary>
public interface IRemoteDatabaseService
{
    /// <summary>
    /// Raised (on a background thread) when the list returned by <see cref="GetRemoteDatabases" /> changes:
    /// a Peer shares or stops sharing a Database, goes online or offline, is renamed, paired or unpaired.
    /// </summary>
    event EventHandler? RemoteDatabasesChanged;

    /// <summary>
    /// Raised (on a background thread) when a Peer reports that a collection or folder of one of its Remote Databases
    /// changed, so any open view of it can re-fetch. Notices arrive while the Peer is online and paired and networking is on.
    /// </summary>
    event EventHandler<RemoteCollectionChangedEventArgs>? RemoteCollectionChanged;

    /// <summary>
    /// Gets the Remote Databases, ordered by Peer name and then Database name. Never contacts a Peer.
    /// </summary>
    IReadOnlyList<RemoteDatabase> GetRemoteDatabases();

    /// <summary>
    /// Gets <paramref name="database" /> (one of <see cref="GetRemoteDatabases" />) as an <see cref="IDatabase" />, so it
    /// is read the same way as a local Database. Its availability is as of <paramref name="database" />; reads still fail
    /// with <see cref="DatabaseUnavailableException" /> if the Peer has gone away since.
    /// </summary>
    IDatabase Open(RemoteDatabase database);

    /// <summary>
    /// Asks every online Peer for its current Shared Databases.
    /// </summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the Remote Database Alias of the Remote Database at <paramref name="address" />: a local label the Peer never
    /// sees; blank clears it, so the Peer's current Database name is shown. Works while the Peer is offline. Kept in the
    /// Peer's record (<see cref="IPeerStore.SetDatabaseAliasAsync" />), so unpairing removes it.
    /// </summary>
    Task SetAliasAsync(RemoteDatabaseAddress address, string? alias, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the collection and folder tree of a Remote Database from its Peer.
    /// </summary>
    /// <exception cref="DatabaseUnavailableException">
    /// The Peer cannot be reached, is not a Peer any more, or no longer shares that Database.
    /// </exception>
    Task<IReadOnlyList<CollectionEntry>> GetCollectionTreeAsync(RemoteDatabaseAddress address, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches one page of the clips (metadata only) of a collection or folder of a Remote Database from its Peer.
    /// Smart and SQL-based collections are evaluated on the Peer with the Peer's own stored SQL.
    /// </summary>
    /// <param name="address">The Remote Database.</param>
    /// <param name="collectionId">The collection.</param>
    /// <param name="folderId">A folder of that collection, or <c>null</c> for the collection itself.</param>
    /// <param name="offset">How many clips to skip.</param>
    /// <param name="count">The most clips to return (the Peer may return fewer).</param>
    /// <param name="cancellationToken">Cancellation token; also cancels the work on the Peer.</param>
    /// <exception cref="DatabaseUnavailableException">
    /// The Peer cannot be reached or did not answer in time, or no longer shares that Database or collection.
    /// </exception>
    Task<ClipPage> GetClipPageAsync(RemoteDatabaseAddress address, Guid collectionId, Guid? folderId, int offset, int count,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches a clip of a Remote Database with all its formats and content from its Peer; large content is streamed
    /// in chunks. Encrypted clips arrive as ciphertext.
    /// </summary>
    /// <returns>The clip and its content, or <c>null</c> if the Peer has no such clip in that Database.</returns>
    /// <exception cref="DatabaseUnavailableException">
    /// The Peer cannot be reached or did not answer in time, or no longer shares that Database.
    /// </exception>
    Task<ClipContent?> GetClipContentAsync(RemoteDatabaseAddress address, Guid clipId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a Remote Search on a Remote Database: the criteria go to the Peer (never SQL), which runs the search itself
    /// and returns at most <see cref="RemoteSearchLimits.MaxResults" /> matches, newest first, with the total match count.
    /// </summary>
    /// <param name="address">The Remote Database.</param>
    /// <param name="filters">What to search for; a scope's collection or folder must be one of that Remote Database.</param>
    /// <param name="cancellationToken">Cancellation token; also stops the search on the Peer.</param>
    /// <exception cref="DatabaseUnavailableException">
    /// The Peer cannot be reached, stopped the search because it took too long, or no longer shares that Database,
    /// collection or folder.
    /// </exception>
    Task<SearchMatches> SearchAsync(RemoteDatabaseAddress address, SearchFilters filters, CancellationToken cancellationToken = default);
}
