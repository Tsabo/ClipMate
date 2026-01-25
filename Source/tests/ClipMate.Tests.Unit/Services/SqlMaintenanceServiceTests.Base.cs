using ClipMate.Data;
using ClipMate.Data.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

public partial class SqlMaintenanceServiceTests
{
    private static (ClipMateDbContext, Mock<ILogger<SqlMaintenanceService>>, SqliteConnection) CreateMocks()
    {
        // Use actual SQLite database for relational features (transactions, connections)
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ClipMateDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new ClipMateDbContext(options);
        dbContext.Database.EnsureCreated();

        var mockLogger = new Mock<ILogger<SqlMaintenanceService>>();
        return (dbContext, mockLogger, connection);
    }

    private static SqlMaintenanceService CreateService(ClipMateDbContext? dbContext = null,
        ILogger<SqlMaintenanceService>? logger = null,
        bool ownsContext = true)
    {
        var (ctx, mockLogger, _) = CreateMocks();
        dbContext ??= ctx;
        logger ??= mockLogger.Object;
        return new SqlMaintenanceService(dbContext, logger, ownsContext);
    }
}
