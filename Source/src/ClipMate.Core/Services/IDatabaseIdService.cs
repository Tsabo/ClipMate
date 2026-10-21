namespace ClipMate.Core.Services;

/// <summary>
/// Reads the permanent Database ID stored inside a Database's file.
/// </summary>
public interface IDatabaseIdService
{
    /// <summary>
    /// Gets the Database ID of a configured Database.
    /// </summary>
    /// <param name="databaseKey">Configuration key of the Database.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The Database ID, or null if the key is unknown, the file does not exist yet, or the Database has not been
    /// opened since the Database ID was introduced.
    /// </returns>
    Task<Guid?> GetDatabaseIdAsync(string databaseKey, CancellationToken cancellationToken = default);
}
