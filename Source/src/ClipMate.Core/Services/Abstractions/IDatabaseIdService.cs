namespace ClipMate.Core.Services.Abstractions;

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

    /// <summary>
    /// Gets the Database ID stored in the database file at <paramref name="filePath" />.
    /// </summary>
    /// <returns>The Database ID, or null if the file does not exist or has no Database ID yet.</returns>
    Task<Guid?> GetDatabaseIdForFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives the database file at <paramref name="filePath" /> a new Database ID, replacing the one it has (e.g. one it
    /// kept when it was copied from another Database). Peers then see it as a different Database.
    /// </summary>
    /// <returns>The new Database ID.</returns>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    Task<Guid> AssignNewDatabaseIdAsync(string filePath, CancellationToken cancellationToken = default);
}
