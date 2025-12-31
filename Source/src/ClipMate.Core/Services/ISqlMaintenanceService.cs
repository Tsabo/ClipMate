using System.Data;

namespace ClipMate.Core.Services;

/// <summary>
/// Service for executing raw SQL maintenance operations within a transaction context.
/// Provides controlled access to database for administrative purposes.
/// </summary>
public interface ISqlMaintenanceService : IAsyncDisposable
{
    /// <summary>
    /// Gets whether a transaction is currently active.
    /// </summary>
    bool HasActiveTransaction { get; }

    /// <summary>
    /// Gets whether the current transaction has been committed.
    /// </summary>
    bool IsTransactionCommitted { get; }

    /// <summary>
    /// Begins a new database transaction.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the current transaction, making all changes permanent.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the current transaction, reverting all changes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a SQL query and returns results as a DataTable.
    /// Use for SELECT, PRAGMA, EXPLAIN queries.
    /// </summary>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the DataTable and row count.</returns>
    Task<SqlQueryResult> ExecuteQueryAsync(string sql, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a SQL command that doesn't return results.
    /// Use for INSERT, UPDATE, DELETE, CREATE, DROP, etc.
    /// </summary>
    /// <param name="sql">The SQL command to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of rows affected.</returns>
    Task<int> ExecuteNonQueryAsync(string sql, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a SQL query execution.
/// </summary>
public sealed class SqlQueryResult
{
    /// <summary>
    /// Gets or sets the result data table.
    /// </summary>
    public DataTable? Data { get; init; }

    /// <summary>
    /// Gets or sets the number of rows returned.
    /// </summary>
    public int RowCount { get; init; }

    /// <summary>
    /// Gets whether the query returned any results.
    /// </summary>
    public bool HasResults => RowCount > 0;
}
