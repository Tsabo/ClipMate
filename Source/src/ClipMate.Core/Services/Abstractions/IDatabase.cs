using ClipMate.Core.Models;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Models.Search;

namespace ClipMate.Core.Services.Abstractions;

/// <summary>
/// One Database, local or remote, read the same way whichever it is. Get one from <see cref="IDatabases" />.
/// <para>
/// Every read throws <see cref="DatabaseUnavailableException" /> when the Database cannot be read right now (a local
/// Database's file is missing, or a Remote Database's Peer is offline or no longer shares it), and
/// <see cref="OperationCanceledException" /> when cancelled.
/// </para>
/// </summary>
public interface IDatabase
{
    /// <summary>
    /// Gets the database key that names this Database in events, tree nodes and settings.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Gets the Database's name: its configured name, or for a Remote Database its Remote Database Alias if it has one.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets whether the Database can be read right now: its file exists, or its Peer is online.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Gets whether nothing may be written to the Database. True for Remote Databases.
    /// </summary>
    bool IsReadOnly { get; }

    /// <summary>
    /// Gets the Remote Database this is, or <c>null</c> for a local Database. Only for features that are about the Peer
    /// (its name and status in the tree, the Remote Database Alias, Copy to local); reading clips, search and refusing
    /// writes never check it.
    /// </summary>
    RemoteDatabase? Remote { get; }

    /// <summary>
    /// Gets the Database's collections and folders as one flat list; a folder's <see cref="CollectionEntry.ParentId" />
    /// is its collection or parent folder.
    /// </summary>
    Task<IReadOnlyList<CollectionEntry>> GetTreeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one page of the clips (metadata only) in <paramref name="scope" />, newest first (the Trash: most recently
    /// deleted first).
    /// </summary>
    /// <exception cref="NotSupportedException">The Database has no such list (a Remote Database has no Trash).</exception>
    Task<ClipPage> GetClipPageAsync(ClipListScope scope, int offset, int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a clip with all its Formats and their content, or <c>null</c> if the Database has no such clip.
    /// </summary>
    Task<ClipContent?> GetClipContentAsync(Guid clipId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches the Database's clips: the newest matches first, at most <paramref name="maxResults" /> of them (a Peer
    /// returns at most <see cref="RemoteSearchLimits.MaxResults" /> whatever is asked), and the total number of matches.
    /// </summary>
    /// <param name="filters">What to search for.</param>
    /// <param name="maxResults">The most clips to return, or <c>null</c> for all of them.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<SearchMatches> SearchAsync(SearchFilters filters, int? maxResults = null, CancellationToken cancellationToken = default);
}
