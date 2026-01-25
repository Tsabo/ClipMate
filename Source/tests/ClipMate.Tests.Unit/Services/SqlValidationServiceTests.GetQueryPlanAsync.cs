using ClipMate.Data;
using ClipMate.Data.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

public partial class SqlValidationServiceTests
{
    [Test]
    [Category("GetQueryPlanAsync")]
    public async Task GetQueryPlanAsync_WithNullOrEmptyQuery_ReturnsErrorMessage()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.GetQueryPlanAsync(string.Empty, _testDatabaseKey);

        // Assert
        await Assert.That(result).IsEqualTo("No query provided");
    }

    [Test]
    [Category("GetQueryPlanAsync")]
    public async Task GetQueryPlanAsync_WithWhitespaceQuery_ReturnsErrorMessage()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.GetQueryPlanAsync("   ", _testDatabaseKey);

        // Assert
        await Assert.That(result).IsEqualTo("No query provided");
    }

    [Test]
    [Category("GetQueryPlanAsync")]
    public async Task GetQueryPlanAsync_WithDatabaseNotLoaded_ReturnsErrorMessage()
    {
        // Arrange
        var mockDbManager = new Mock<IDatabaseManager>();
        mockDbManager.Setup(p => p.CreateDatabaseContext(It.IsAny<string>()))
            .Returns((ClipMateDbContext?)null);

        var service = new SqlValidationService(
            mockDbManager.Object,
            new Mock<ILogger<SqlValidationService>>().Object);

        // Act
        var result = await service.GetQueryPlanAsync("SELECT * FROM Clips", _testDatabaseKey);

        // Assert
        await Assert.That(result).Contains("Database");
        await Assert.That(result).Contains("not loaded");
    }

    [Test]
    [Category("GetQueryPlanAsync")]
    public async Task GetQueryPlanAsync_WithValidQuery_ReturnsQueryPlan()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ClipMateDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        // Create test schema
        await using (var context = new ClipMateDbContext(options))
        {
            context.Database.SetDbConnection(connection);
            await context.Database.EnsureCreatedAsync();
        }

        var mockDbManager = new Mock<IDatabaseManager>();
        mockDbManager.Setup(p => p.CreateDatabaseContext(_testDatabaseKey))
            .Returns(() =>
            {
                var ctx = new ClipMateDbContext(options);
                ctx.Database.SetDbConnection(connection);
                return ctx;
            });

        var service = new SqlValidationService(
            mockDbManager.Object,
            new Mock<ILogger<SqlValidationService>>().Object);

        // Act
        var result = await service.GetQueryPlanAsync("SELECT * FROM Clips", _testDatabaseKey);

        // Assert
        await Assert.That(result).IsNotEmpty();
        await Assert.That(result).Contains("SCAN"); // SQLite query plans typically contain SCAN or SEARCH

        // Cleanup
        await connection.CloseAsync();
    }

    [Test]
    [Category("GetQueryPlanAsync")]
    public async Task GetQueryPlanAsync_WithInvalidSyntax_ReturnsErrorMessage()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ClipMateDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        // Create test schema
        await using (var context = new ClipMateDbContext(options))
        {
            context.Database.SetDbConnection(connection);
            await context.Database.EnsureCreatedAsync();
        }

        var mockDbManager = new Mock<IDatabaseManager>();
        mockDbManager.Setup(p => p.CreateDatabaseContext(_testDatabaseKey))
            .Returns(() =>
            {
                var ctx = new ClipMateDbContext(options);
                ctx.Database.SetDbConnection(connection);
                return ctx;
            });

        var service = new SqlValidationService(
            mockDbManager.Object,
            new Mock<ILogger<SqlValidationService>>().Object);

        // Act
        var result = await service.GetQueryPlanAsync("INVALID SQL QUERY", _testDatabaseKey);

        // Assert
        await Assert.That(result).StartsWith("Error:");

        // Cleanup
        await connection.CloseAsync();
    }

    [Test]
    [Category("GetQueryPlanAsync")]
    public async Task GetQueryPlanAsync_WithComplexQuery_ReturnsDetailedPlan()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ClipMateDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        // Create test schema
        await using (var context = new ClipMateDbContext(options))
        {
            context.Database.SetDbConnection(connection);
            await context.Database.EnsureCreatedAsync();
        }

        var mockDbManager = new Mock<IDatabaseManager>();
        mockDbManager.Setup(p => p.CreateDatabaseContext(_testDatabaseKey))
            .Returns(() =>
            {
                var ctx = new ClipMateDbContext(options);
                ctx.Database.SetDbConnection(connection);
                return ctx;
            });

        var service = new SqlValidationService(
            mockDbManager.Object,
            new Mock<ILogger<SqlValidationService>>().Object);

        // Act - Use CapturedAt instead of CreatedAt
        var result = await service.GetQueryPlanAsync(
            "SELECT * FROM Clips WHERE Id > '00000000-0000-0000-0000-000000000000' ORDER BY CapturedAt DESC LIMIT 10",
            _testDatabaseKey);

        // Assert
        await Assert.That(result).IsNotEmpty();
        await Assert.That(result).Contains("Clips"); // Table name should appear in plan

        // Cleanup
        await connection.CloseAsync();
    }
}
