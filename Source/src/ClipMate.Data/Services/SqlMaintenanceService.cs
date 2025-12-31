using System.Data;
using ClipMate.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Service for executing raw SQL maintenance operations within a transaction context.
/// Provides controlled access to database for administrative purposes.
/// </summary>
internal sealed class SqlMaintenanceService : ISqlMaintenanceService
{
    private readonly ClipMateDbContext _dbContext;
    private readonly ILogger<SqlMaintenanceService> _logger;
    private readonly bool _ownsContext;
    private bool _disposed;
    private IDbContextTransaction? _transaction;

    /// <summary>
    /// Creates a new SqlMaintenanceService that owns and will dispose the provided context.
    /// </summary>
    public SqlMaintenanceService(ClipMateDbContext dbContext, ILogger<SqlMaintenanceService> logger)
        : this(dbContext, logger, true)
    {
    }

    /// <summary>
    /// Creates a new SqlMaintenanceService with explicit ownership control.
    /// </summary>
    internal SqlMaintenanceService(ClipMateDbContext dbContext, ILogger<SqlMaintenanceService> logger, bool ownsContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ownsContext = ownsContext;
    }

    /// <inheritdoc />
    public bool HasActiveTransaction => _transaction != null;

    /// <inheritdoc />
    public bool IsTransactionCommitted { get; private set; }

    /// <inheritdoc />
    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_transaction != null)
            throw new InvalidOperationException("A transaction is already active. Commit or rollback before starting a new one.");

        _transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        IsTransactionCommitted = false;
        _logger.LogInformation("SQL Maintenance: Transaction started");
    }

    /// <inheritdoc />
    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_transaction == null)
            throw new InvalidOperationException("No active transaction to commit.");

        await _transaction.CommitAsync(cancellationToken);
        IsTransactionCommitted = true;
        _logger.LogInformation("SQL Maintenance: Transaction committed");

        await _transaction.DisposeAsync();
        _transaction = null;
    }

    /// <inheritdoc />
    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_transaction == null)
            throw new InvalidOperationException("No active transaction to rollback.");

        await _transaction.RollbackAsync(cancellationToken);
        IsTransactionCommitted = false;
        _logger.LogInformation("SQL Maintenance: Transaction rolled back");

        await _transaction.DisposeAsync();
        _transaction = null;
    }

    /// <inheritdoc />
    public async Task<SqlQueryResult> ExecuteQueryAsync(string sql, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var dataTable = new DataTable();
        dataTable.Load(reader);

        _logger.LogInformation("SQL query executed: {RowCount} rows returned", dataTable.Rows.Count);

        return new SqlQueryResult
        {
            Data = dataTable,
            RowCount = dataTable.Rows.Count,
        };
    }

    /// <inheritdoc />
    public async Task<int> ExecuteNonQueryAsync(string sql, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogInformation("SQL command executed: {RowsAffected} rows affected", rowsAffected);

        return rowsAffected;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        if (_transaction != null)
        {
            if (!IsTransactionCommitted)
            {
                try
                {
                    await _transaction.RollbackAsync();
                    _logger.LogInformation("SQL Maintenance: Transaction rolled back on dispose");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error rolling back transaction on dispose");
                }
            }

            await _transaction.DisposeAsync();
            _transaction = null;
        }

        if (_ownsContext)
            await _dbContext.DisposeAsync();

        _disposed = true;
    }
}
