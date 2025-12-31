namespace ClipMate.Core.Services;

/// <summary>
/// Factory for creating SQL maintenance service instances.
/// Each instance manages its own transaction context.
/// </summary>
public interface ISqlMaintenanceServiceFactory
{
    /// <summary>
    /// Creates a new SQL maintenance service instance for the specified database.
    /// The caller is responsible for disposing the returned instance.
    /// </summary>
    /// <param name="databaseKey">The database key to operate on.</param>
    /// <returns>A new SQL maintenance service instance.</returns>
    ISqlMaintenanceService Create(string databaseKey);
}
