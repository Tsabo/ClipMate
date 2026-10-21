using ClipMate.Core.Models.Networking;

namespace ClipMate.Core.Services;

/// <summary>
/// The requesting side of Shared Databases: the Remote Databases this Device can browse, one for each Shared Database
/// of each Peer, and read access to them (ADR 0001: a read-only live proxy; no SQL crosses the wire).
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
    /// Gets the Remote Databases, ordered by Peer name and then Database name. Never contacts a Peer.
    /// </summary>
    IReadOnlyList<RemoteDatabase> GetRemoteDatabases();

    /// <summary>
    /// Asks every online Peer for its current Shared Databases.
    /// </summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the collection and folder tree of a Remote Database from its Peer.
    /// </summary>
    /// <exception cref="RemoteDatabaseUnavailableException">
    /// The Peer cannot be reached, is not a Peer any more, or no longer shares that Database.
    /// </exception>
    Task<IReadOnlyList<SharedCollection>> GetCollectionTreeAsync(RemoteDatabaseAddress address, CancellationToken cancellationToken = default);
}

/// <summary>
/// Thrown when a Remote Database cannot be read right now.
/// </summary>
public sealed class RemoteDatabaseUnavailableException : Exception
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    public RemoteDatabaseUnavailableException()
    {
    }

    /// <summary>
    /// Creates the exception with a message.
    /// </summary>
    public RemoteDatabaseUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates the exception with a message and the underlying failure.
    /// </summary>
    public RemoteDatabaseUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
