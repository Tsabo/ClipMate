using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Models.Search;

namespace ClipMate.Core.Services.Abstractions;

/// <summary>
/// The serving side of Shared Databases: which local Databases this Device shares with its Peers, and what it serves
/// from them. Only Databases whose Share with Peers setting is on are ever served; nothing else (templates, shortcuts,
/// application profiles, users, settings, editor state, SQL) is reachable through this service.
/// </summary>
public interface ISharedDatabaseService
{
    /// <summary>
    /// Gets the Shared Databases. A Database without a Database ID yet, or whose Database ID another Shared Database
    /// already uses, is left out.
    /// </summary>
    Task<IReadOnlyList<SharedDatabase>> GetSharedDatabasesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the collection and folder tree of the Shared Database with <paramref name="databaseId" />, or <c>null</c>
    /// if no Shared Database has that Database ID.
    /// </summary>
    Task<IReadOnlyList<CollectionEntry>?> GetCollectionTreeAsync(Guid databaseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one page of the clips (metadata only) of a collection or folder of a Shared Database. Smart and SQL-based
    /// collections are evaluated with their own stored SQL; no SQL is ever accepted from the caller.
    /// </summary>
    /// <param name="databaseId">The Shared Database.</param>
    /// <param name="collectionId">A collection of that Database.</param>
    /// <param name="folderId">A folder of that Database, or <c>null</c> for the collection itself.</param>
    /// <param name="offset">How many clips to skip.</param>
    /// <param name="count">The most clips to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The page, or <c>null</c> if no Shared Database has that Database ID or it has no such collection or folder.</returns>
    Task<ClipPage?> GetClipPageAsync(Guid databaseId, Guid collectionId, Guid? folderId, int offset, int count,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a clip of a Shared Database with all its formats and content as stored: encrypted clips stay ciphertext.
    /// </summary>
    /// <returns>The clip, or <c>null</c> if no Shared Database has that Database ID or it has no such (undeleted) clip.</returns>
    Task<ClipContent?> GetClipContentAsync(Guid databaseId, Guid clipId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a Remote Search on a Shared Database: the Peer builds and runs parameterized SQL from
    /// <paramref name="filters" /> itself (no SQL is accepted from the caller) and returns the newest
    /// <see cref="RemoteSearchLimits.MaxResults" /> matches at most, with the total number of matches. Deleted clips and
    /// shortcuts are never served, whatever the filters ask for. Nothing about the search is kept afterwards.
    /// </summary>
    /// <param name="databaseId">The Shared Database.</param>
    /// <param name="filters">What to search for; the collection or folder of a scope must belong to that Database.</param>
    /// <param name="cancellationToken">Cancellation token; stops the query.</param>
    /// <returns>
    /// The matches, or <c>null</c> if no Shared Database has that Database ID or it has no such collection or folder.
    /// </returns>
    /// <exception cref="ArgumentException">The scope needs a collection or folder ID that <paramref name="filters" /> lacks.</exception>
    Task<SearchMatches?> SearchAsync(Guid databaseId, SearchFilters filters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a Shared Database among <paramref name="otherDatabases" /> whose Database ID is the same as the one stored
    /// in <paramref name="filePath" /> (a copied database file keeps its Database ID). Peers could not tell two such
    /// Databases apart, so the file at <paramref name="filePath" /> must not be shared until it has a new Database ID.
    /// </summary>
    /// <param name="filePath">The database file about to be shared.</param>
    /// <param name="otherDatabases">The other configured Databases (not including the one about to be shared).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The conflicting Shared Database, or <c>null</c> if there is none.</returns>
    Task<DatabaseConfiguration?> FindDatabaseIdConflictAsync(string filePath, IEnumerable<DatabaseConfiguration> otherDatabases,
        CancellationToken cancellationToken = default);
}
