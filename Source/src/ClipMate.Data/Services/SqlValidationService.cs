using System.Data;
using System.Text;
using ClipMate.Core.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Service for validating SQL queries to prevent dangerous operations.
/// </summary>
public class SqlValidationService : ISqlValidationService
{
    // SQL validation - prevent dangerous operations
    private static readonly string[] DangerousKeywords =
    [
        "DROP", "DELETE", "UPDATE", "INSERT", "ALTER", "CREATE", "TRUNCATE", "EXEC", "EXECUTE", "PRAGMA",
    ];

    private readonly IDatabaseManager _databaseManager;
    private readonly ILogger<SqlValidationService> _logger;

    public SqlValidationService(IDatabaseManager databaseManager, ILogger<SqlValidationService> logger)
    {
        _databaseManager = databaseManager ?? throw new ArgumentNullException(nameof(databaseManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<(bool IsValid, string ErrorMessage)> ValidateSqlQueryAsync(string sqlQuery,
        string databaseKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sqlQuery))
            return (false, "SQL query cannot be empty");

        // Check for dangerous keywords
        var upperQuery = sqlQuery.ToUpperInvariant();
        foreach (var keyword in DangerousKeywords)
        {
            if (upperQuery.Contains(keyword))
                return (false, $"SQL query contains dangerous keyword: {keyword}");
        }

        // Get database context for validation
        var context = _databaseManager.GetDatabaseContext(databaseKey);
        if (context == null)
            return (false, $"Database '{databaseKey}' is not loaded");

        // Validate syntax by running EXPLAIN QUERY PLAN
        try
        {
            var connection = context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = $"EXPLAIN QUERY PLAN {sqlQuery}";

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            // If we can execute EXPLAIN without error, syntax is valid
            while (await reader.ReadAsync(cancellationToken))
            {
                // Just consume the results to ensure query is syntactically valid
            }

            return (true, string.Empty);
        }
        catch (SqliteException ex)
        {
            _logger.LogWarning(ex, "SQL query validation failed for query: {Query}", sqlQuery);
            return (false, $"SQL syntax error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during SQL query validation");
            return (false, $"Validation error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<string> GetQueryPlanAsync(string sqlQuery,
        string databaseKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sqlQuery))
            return "No query provided";

        var context = _databaseManager.GetDatabaseContext(databaseKey);
        if (context == null)
            return $"Database '{databaseKey}' is not loaded";

        try
        {
            var connection = context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = $"EXPLAIN QUERY PLAN {sqlQuery}";

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var plan = new StringBuilder();
            while (await reader.ReadAsync(cancellationToken))
            {
                // SQLite EXPLAIN QUERY PLAN returns: id, parent, notused, detail
                var detail = reader.GetString(3);
                plan.AppendLine(detail);
            }

            return plan.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting query plan for: {Query}", sqlQuery);
            return $"Error: {ex.Message}";
        }
    }
}
