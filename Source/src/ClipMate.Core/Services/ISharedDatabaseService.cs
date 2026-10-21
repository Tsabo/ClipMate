using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Networking;

namespace ClipMate.Core.Services;

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
    Task<IReadOnlyList<SharedCollection>?> GetCollectionTreeAsync(Guid databaseId, CancellationToken cancellationToken = default);

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
