using System.Data;
using ClipMate.Data;
using ClipMate.Data.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Integration tests for DatabaseSchemaMigrationService.
/// Tests run sequentially to avoid database file conflicts.
/// </summary>
[NotInParallel]
public class DatabaseSchemaMigrationServiceTests : IntegrationTestBase
{
    private DatabaseSchemaMigrationService CreateService(ILogger<DatabaseSchemaMigrationService>? logger = null) => new(logger);

    [Test]
    [Category("Integration")]
    [Category("DatabaseSchemaMigrationService")]
    public async Task Constructor_WithNullLogger_CreatesInstance()
    {
        // Act
        var service = new DatabaseSchemaMigrationService();

        // Assert
        await Assert.That(service).IsNotNull();
    }

    [Test]
    [Category("Integration")]
    [Category("DatabaseSchemaMigrationService")]
    public async Task Constructor_WithLogger_CreatesInstance()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<DatabaseSchemaMigrationService>>();

        // Act
        var service = new DatabaseSchemaMigrationService(mockLogger.Object);

        // Assert
        await Assert.That(service).IsNotNull();
    }

    [Test]
    [Category("Integration")]
    [Category("DatabaseSchemaMigrationService")]
    public async Task MigrateAsync_WithNewDatabase_AppliesSchema()
    {
        // Arrange
        var service = CreateService();
        var tempPath = Path.Combine(Path.GetTempPath(), $"schema_test_{Guid.NewGuid()}.db");

        try
        {
            await using var connection = new SqliteConnection($"Data Source={tempPath}");
            // Create empty database
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<ClipMateDbContext>()
                .UseSqlite(connection)
                .Options;

            await using (var context = new ClipMateDbContext(options))
            {
                // Act - This should create the full schema
                await service.MigrateAsync(context);

                // Assert - Verify key tables exist
                var tables = await GetTableNamesAsync(connection);
                
                // Core tables that should always exist
                await Assert.That(tables).Contains("Clips");
                await Assert.That(tables).Contains("Collections");
                // Note: Table name is "ShortCut" (singular) in the database schema
                await Assert.That(tables).Contains("ShortCut");
            }

            // Ensure connection is fully closed and disposed
            await connection.CloseAsync();
        }
        finally
        {
            CleanupDatabase(tempPath);
        }
    }

    [Test]
    [Category("Integration")]
    [Category("DatabaseSchemaMigrationService")]
    public async Task MigrateAsync_WithExistingSchema_NoChanges()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<DatabaseSchemaMigrationService>>();
        var service = new DatabaseSchemaMigrationService(mockLogger.Object);
        var tempPath = Path.Combine(Path.GetTempPath(), $"schema_test_{Guid.NewGuid()}.db");

        try
        {
            await using var connection = new SqliteConnection($"Data Source={tempPath}");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<ClipMateDbContext>()
                .UseSqlite(connection)
                .Options;

            await using (var context = new ClipMateDbContext(options))
            {
                await context.Database.EnsureCreatedAsync();

                // Act - Migrate when schema is already up to date
                await service.MigrateAsync(context);
            }

            // Verify logging
            mockLogger.Verify(
                p => p.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("schema is up to date")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            await connection.CloseAsync();
        }
        finally
        {
            CleanupDatabase(tempPath);
        }
    }

    [Test]
    [Category("Integration")]
    [Category("DatabaseSchemaMigrationService")]
    public async Task MigrateAsync_CreatesBackupBeforeMigration()
    {
        // Arrange
        var service = CreateService();
        var tempPath = Path.Combine(Path.GetTempPath(), $"backup_test_{Guid.NewGuid()}.db");

        try
        {
            await using var connection = new SqliteConnection($"Data Source={tempPath}");
            // Create a database with minimal schema to force migration
            await connection.OpenAsync();

            // Create a simple table that will differ from the EF schema
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "CREATE TABLE TestTable (Id INTEGER PRIMARY KEY)";
                await cmd.ExecuteNonQueryAsync();
            }

            await connection.CloseAsync();

            // Now open with EF context
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<ClipMateDbContext>()
                .UseSqlite(connection)
                .Options;

            await using (var context = new ClipMateDbContext(options))
            {
                // Act
                await service.MigrateAsync(context);
            }

            // Assert - Backup directory should exist with backup file
            var databaseDirectory = Path.GetDirectoryName(tempPath)!;
            var backupDirectory = Path.Combine(databaseDirectory, "MigrationBackups");

            await Assert.That(Directory.Exists(backupDirectory)).IsTrue();

            var backupFiles = Directory.GetFiles(backupDirectory, "backup_test_*-migration-*.db");
            await Assert.That(backupFiles.Length).IsGreaterThan(0);

            // Cleanup backups
            if (Directory.Exists(backupDirectory))
                Directory.Delete(backupDirectory, true);

            await connection.CloseAsync();
        }
        finally
        {
            CleanupDatabase(tempPath);
        }
    }

    [Test]
    [Category("Integration")]
    [Category("DatabaseSchemaMigrationService")]
    public async Task MigrateAsync_WithInMemoryDatabase_SkipsMigration()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<DatabaseSchemaMigrationService>>();
        var service = new DatabaseSchemaMigrationService(mockLogger.Object);

        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ClipMateDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new ClipMateDbContext(options);
        await context.Database.EnsureCreatedAsync();

        // Act
        await service.MigrateAsync(context);

        // Assert - Should successfully handle in-memory database
        await Assert.That(context.Database.GetConnectionString()).IsNotNull();
    }

    [Test]
    [Category("Integration")]
    [Category("DatabaseSchemaMigrationService")]
    public async Task MigrateAsync_WithClosedConnection_OpensAndCloses()
    {
        // Arrange
        var service = CreateService();
        var tempPath = Path.Combine(Path.GetTempPath(), $"connection_test_{Guid.NewGuid()}.db");

        try
        {
            await using var connection = new SqliteConnection($"Data Source={tempPath}");
            // Don't open connection

            var options = new DbContextOptionsBuilder<ClipMateDbContext>()
                .UseSqlite(connection)
                .Options;

            await using (var context = new ClipMateDbContext(options))
            {
                // Act
                await service.MigrateAsync(context);

                // Assert - Connection should be closed after migration
                await Assert.That(connection.State).IsEqualTo(ConnectionState.Closed);
            }

            await connection.CloseAsync();
        }
        finally
        {
            CleanupDatabase(tempPath);
        }
    }

    [Test]
    [Category("Integration")]
    [Category("DatabaseSchemaMigrationService")]
    public async Task MigrateAsync_WithOpenConnection_LeavesOpen()
    {
        // Arrange
        var service = CreateService();
        var tempPath = Path.Combine(Path.GetTempPath(), $"connection_test_{Guid.NewGuid()}.db");

        try
        {
            await using var connection = new SqliteConnection($"Data Source={tempPath}");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<ClipMateDbContext>()
                .UseSqlite(connection)
                .Options;

            await using (var context = new ClipMateDbContext(options))
            {
                // Act
                await service.MigrateAsync(context);

                // Assert - Connection should still be open
                await Assert.That(connection.State).IsEqualTo(ConnectionState.Open);
            }

            await connection.CloseAsync();
        }
        finally
        {
            CleanupDatabase(tempPath);
        }
    }

    [Test]
    [Category("Integration")]
    [Category("DatabaseSchemaMigrationService")]
    public async Task MigrateAsync_LogsSchemaChanges()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<DatabaseSchemaMigrationService>>();
        var service = new DatabaseSchemaMigrationService(mockLogger.Object);
        var tempPath = Path.Combine(Path.GetTempPath(), $"logging_test_{Guid.NewGuid()}.db");

        try
        {
            await using var connection = new SqliteConnection($"Data Source={tempPath}");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<ClipMateDbContext>()
                .UseSqlite(connection)
                .Options;

            await using (var context = new ClipMateDbContext(options))
            {
                // Act
                await service.MigrateAsync(context);
            }

            // Assert - Should log starting migration
            mockLogger.Verify(
                p => p.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting database schema migration")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);

            await connection.CloseAsync();
        }
        finally
        {
            CleanupDatabase(tempPath);
        }
    }

    private static async Task<List<string>> GetTableNamesAsync(SqliteConnection connection)
    {
        var tables = new List<string>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            tables.Add(reader.GetString(0));

        return tables;
    }

    private static void CleanupDatabase(string path)
    {
        if (!File.Exists(path))
            return;

        // Clear all SQLite connection pools and force GC
        SqliteConnection.ClearAllPools();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        for (int i = 0; i < 20; i++)
        {
            try
            {
                Thread.Sleep(100 * (i + 1));
                File.Delete(path);
                return;
            }
            catch (IOException)
            {
                if (i == 19)
                    throw;

                SqliteConnection.ClearAllPools();
            }
        }
    }
}
