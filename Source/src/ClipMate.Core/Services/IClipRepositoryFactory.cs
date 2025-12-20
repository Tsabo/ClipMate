using ClipMate.Core.Repositories;

namespace ClipMate.Core.Services;

/// <summary>
/// Factory for creating IClipRepository instances for specific databases.
/// Encapsulates the complexity of database context resolution and repository construction.
/// </summary>
public interface IClipRepositoryFactory
{
    /// <summary>
    /// Creates a repository instance for the specified database.
    /// </summary>
    /// <param name="databaseKey">The database configuration key (e.g., "db_a1b2c3d4").</param>
    /// <returns>A repository instance bound to the specified database context.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the database context cannot be found.</exception>
    IClipRepository CreateRepository(string databaseKey);
}
