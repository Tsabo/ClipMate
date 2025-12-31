using ClipMate.Core.Services;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Factory for creating SQL maintenance service instances.
/// Each instance manages its own transaction context with a fresh DbContext.
/// </summary>
public sealed class SqlMaintenanceServiceFactory : ISqlMaintenanceServiceFactory
{
    private readonly IDatabaseContextFactory _contextFactory;
    private readonly ILoggerFactory _loggerFactory;

    public SqlMaintenanceServiceFactory(IDatabaseContextFactory contextFactory, ILoggerFactory loggerFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    /// <inheritdoc />
    public ISqlMaintenanceService Create(string databaseKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseKey);

        var context = _contextFactory.CreateContext(databaseKey);
        var logger = _loggerFactory.CreateLogger<SqlMaintenanceService>();

        return new SqlMaintenanceService(context, logger);
    }
}
