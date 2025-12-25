namespace ClipMate.Core.Services;

/// <summary>
/// Service for database setup and initialization tasks.
/// Used during application startup and first-run setup wizard.
/// </summary>
public interface ISetupService
{
    /// <summary>
    /// Creates a new ClipMate database at the specified path.
    /// </summary>
    /// <param name="databasePath">The full path to the database file to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if database was created successfully, false otherwise.</returns>
    Task<bool> CreateDatabaseAsync(string databasePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that a database exists and has the correct schema.
    /// </summary>
    /// <param name="databasePath">The full path to the database file to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if database is valid, false otherwise.</returns>
    Task<bool> ValidateDatabaseAsync(string databasePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the default database path for the application.
    /// </summary>
    /// <returns>The full path to the default database location.</returns>
    string GetDefaultDatabasePath();

    /// <summary>
    /// Ensures the default database exists, creating it if necessary.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The path to the default database.</returns>
    Task<string> EnsureDefaultDatabaseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Seeds default data (collections, etc.) into an existing database and applies schema migrations.
    /// </summary>
    /// <param name="databasePath">The full path to the database file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SeedDefaultDataAsync(string databasePath, CancellationToken cancellationToken = default);
}
