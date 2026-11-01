namespace ClipMate.Core.Services.Abstractions;

/// <summary>
/// Every Database this Device can read: the configured local Databases and the Remote Databases its Peers share.
/// </summary>
public interface IDatabases
{
    /// <summary>
    /// Gets every Database: the local ones in configured order, then the Remote Databases.
    /// </summary>
    IReadOnlyList<IDatabase> All { get; }

    /// <summary>
    /// Raised when Databases are added, removed or renamed, or a Remote Database becomes available or unavailable.
    /// </summary>
    event EventHandler? Changed;

    /// <summary>
    /// Gets the Database a database key names.
    /// </summary>
    /// <exception cref="DatabaseUnavailableException">
    /// The key names a Remote Database that is no longer shared, or a local Database that is no longer configured.
    /// </exception>
    IDatabase Get(string databaseKey);
}
