namespace ClipMate.Core.Services;

/// <summary>
/// Service for validating SQL queries to prevent dangerous operations.
/// Used by SearchService and VirtualCollectionService to ensure user-provided SQL is safe.
/// </summary>
public interface ISqlValidationService
{
    /// <summary>
    /// Validates a SQL query for safety and syntax.
    /// </summary>
    /// <param name="sqlQuery">The SQL query to validate.</param>
    /// <param name="databaseKey">The database key to validate against.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing whether the query is valid and an error message if not.</returns>
    Task<(bool IsValid, string ErrorMessage)> ValidateSqlQueryAsync(string sqlQuery,
        string databaseKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the query execution plan for a SQL query (EXPLAIN QUERY PLAN).
    /// </summary>
    /// <param name="sqlQuery">The SQL query to analyze.</param>
    /// <param name="databaseKey">The database key to analyze against.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The query execution plan as a string.</returns>
    Task<string> GetQueryPlanAsync(string sqlQuery,
        string databaseKey,
        CancellationToken cancellationToken = default);
}
