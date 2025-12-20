using ClipMate.Data.Schema.Abstractions;
using ClipMate.Data.Schema.Models;
using ClipMate.Data.Schema.Sqlite;
using Microsoft.Data.Sqlite;
using Moq;

namespace ClipMate.Tests.Unit.Schema;

public class SqliteSchemaMigratorTests
{
    // ===== Successful Migration Tests =====

    [Test]
    public async Task MigrateAsync_EmptyDiff_ReturnsSuccess()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var migrator = new SqliteSchemaMigrator(connection);
        var diff = new SchemaDiff();

        // Act
        var result = await migrator.MigrateAsync(diff, false);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.SqlExecuted).IsEmpty();
        await Assert.That(result.Errors).IsEmpty();
    }

    [Test]
    public async Task MigrateAsync_CreateTableOperation_ExecutesSuccessfully()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var migrator = new SqliteSchemaMigrator(connection);
        var diff = new SchemaDiff
        {
            Operations =
            [
                new MigrationOperation
                {
                    Type = MigrationOperationType.CreateTable,
                    TableName = "Users",
                    Sql = "CREATE TABLE Users (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL)",
                },
            ],
        };

        // Act
        var result = await migrator.MigrateAsync(diff);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.SqlExecuted).Count().IsEqualTo(1);
        await Assert.That(result.Errors).IsEmpty();

        // Verify table exists
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Users'";
        var tableName = await cmd.ExecuteScalarAsync();
        await Assert.That(tableName).IsEqualTo("Users");
    }

    [Test]
    public async Task MigrateAsync_MultipleOperations_ExecutesInOrder()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var migrator = new SqliteSchemaMigrator(connection);
        var diff = new SchemaDiff
        {
            Operations =
            [
                new MigrationOperation
                {
                    Type = MigrationOperationType.CreateTable,
                    TableName = "Users",
                    Sql = "CREATE TABLE Users (Id INTEGER PRIMARY KEY)",
                },

                new MigrationOperation
                {
                    Type = MigrationOperationType.AddColumn,
                    TableName = "Users",
                    ColumnName = "Email",
                    Sql = "ALTER TABLE Users ADD COLUMN Email TEXT",
                },

                new MigrationOperation
                {
                    Type = MigrationOperationType.CreateIndex,
                    IndexName = "IX_Users_Email",
                    Sql = "CREATE INDEX IX_Users_Email ON Users (Email)",
                },
            ],
        };

        // Act
        var result = await migrator.MigrateAsync(diff);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.SqlExecuted).Count().IsEqualTo(3);
        await Assert.That(result.Errors).IsEmpty();
    }

    // ===== Dry Run Tests =====

    [Test]
    public async Task MigrateAsync_DryRunMode_DoesNotExecuteSQL()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var migrator = new SqliteSchemaMigrator(connection);
        var diff = new SchemaDiff
        {
            Operations =
            [
                new MigrationOperation
                {
                    Type = MigrationOperationType.CreateTable,
                    TableName = "Users",
                    Sql = "CREATE TABLE Users (Id INTEGER PRIMARY KEY)",
                },
            ],
        };

        // Act
        var result = await migrator.MigrateAsync(diff, true);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.SqlExecuted).Count().IsEqualTo(1);
        await Assert.That(result.SqlExecuted[0]).Contains("CREATE TABLE Users");

        // Verify table does NOT exist
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Users'";
        var tableName = await cmd.ExecuteScalarAsync();
        await Assert.That(tableName).IsNull();
    }

    // ===== Transaction and Rollback Tests =====

    [Test]
    public async Task MigrateAsync_InvalidSQL_RollsBackTransaction()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        // Create initial table
        using var setupCmd = connection.CreateCommand();
        setupCmd.CommandText = "CREATE TABLE ExistingTable (Id INTEGER PRIMARY KEY)";
        await setupCmd.ExecuteNonQueryAsync();

        var migrator = new SqliteSchemaMigrator(connection);
        var diff = new SchemaDiff
        {
            Operations =
            [
                new MigrationOperation
                {
                    Type = MigrationOperationType.CreateTable,
                    TableName = "Users",
                    Sql = "CREATE TABLE Users (Id INTEGER PRIMARY KEY)",
                },

                new MigrationOperation
                {
                    Type = MigrationOperationType.CreateTable,
                    TableName = "InvalidTable",
                    Sql = "INVALID SQL SYNTAX HERE",
                },
            ],
        };

        // Act
        var result = await migrator.MigrateAsync(diff);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Errors).Count().IsGreaterThan(0);

        // Verify Users table does NOT exist (rollback happened)
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Users'";
        var tableName = await cmd.ExecuteScalarAsync();
        await Assert.That(tableName).IsNull();

        // Verify existing table still exists
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='ExistingTable'";
        var existingTable = await cmd.ExecuteScalarAsync();
        await Assert.That(existingTable).IsEqualTo("ExistingTable");
    }

    // ===== Hook Tests =====

    [Test]
    public async Task MigrateAsync_WithHook_CallsOnBeforeMigration()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var mockHook = new Mock<IMigrationHook>();
        var migrator = new SqliteSchemaMigrator(connection, mockHook.Object);
        var diff = new SchemaDiff
        {
            Operations =
            [
                new MigrationOperation
                {
                    Type = MigrationOperationType.CreateTable,
                    TableName = "Users",
                    Sql = "CREATE TABLE Users (Id INTEGER PRIMARY KEY)",
                },
            ],
        };

        // Act
        await migrator.MigrateAsync(diff);

        // Assert
        mockHook.Verify(p => p.OnBeforeMigrationAsync(It.IsAny<MigrationContext>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task MigrateAsync_SuccessfulMigration_CallsOnAfterMigration()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var mockHook = new Mock<IMigrationHook>();
        var migrator = new SqliteSchemaMigrator(connection, mockHook.Object);
        var diff = new SchemaDiff
        {
            Operations =
            [
                new MigrationOperation
                {
                    Type = MigrationOperationType.CreateTable,
                    TableName = "Users",
                    Sql = "CREATE TABLE Users (Id INTEGER PRIMARY KEY)",
                },
            ],
        };

        // Act
        await migrator.MigrateAsync(diff);

        // Assert
        mockHook.Verify(p => p.OnAfterMigrationAsync(
                It.Is<MigrationContext>(ctx => !ctx.IsDryRun),
                It.Is<MigrationResult>(r => r.Success),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task MigrateAsync_FailedMigration_DoesNotCallOnAfterMigration()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var mockHook = new Mock<IMigrationHook>();
        var migrator = new SqliteSchemaMigrator(connection, mockHook.Object);
        var diff = new SchemaDiff
        {
            Operations =
            [
                new MigrationOperation
                {
                    Type = MigrationOperationType.CreateTable,
                    TableName = "Invalid",
                    Sql = "INVALID SQL",
                },
            ],
        };

        // Act
        await migrator.MigrateAsync(diff);

        // Assert
        mockHook.Verify(p => p.OnAfterMigrationAsync(
                It.IsAny<MigrationContext>(),
                It.IsAny<MigrationResult>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task MigrateAsync_DryRun_CallsBothHooks()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var mockHook = new Mock<IMigrationHook>();
        var migrator = new SqliteSchemaMigrator(connection, mockHook.Object);
        var diff = new SchemaDiff
        {
            Operations =
            [
                new MigrationOperation
                {
                    Type = MigrationOperationType.CreateTable,
                    TableName = "Users",
                    Sql = "CREATE TABLE Users (Id INTEGER PRIMARY KEY)",
                },
            ],
        };

        // Act
        await migrator.MigrateAsync(diff, true);

        // Assert
        mockHook.Verify(p => p.OnBeforeMigrationAsync(
                It.Is<MigrationContext>(ctx => ctx.IsDryRun),
                It.IsAny<CancellationToken>()),
            Times.Once);

        mockHook.Verify(p => p.OnAfterMigrationAsync(
                It.Is<MigrationContext>(ctx => ctx.IsDryRun),
                It.Is<MigrationResult>(r => r.Success),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ===== Warning Tests =====

    [Test]
    public async Task MigrateAsync_WithWarnings_IncludesWarningsInResult()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var migrator = new SqliteSchemaMigrator(connection);
        var diff = new SchemaDiff
        {
            Operations =
            [
                new MigrationOperation
                {
                    Type = MigrationOperationType.CreateTable,
                    TableName = "Users",
                    Sql = "CREATE TABLE Users (Id INTEGER PRIMARY KEY)",
                },
            ],
            Warnings = ["Warning: Schema change may cause data loss"],
        };

        // Act
        var result = await migrator.MigrateAsync(diff);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Warnings).Count().IsEqualTo(1);
        await Assert.That(result.Warnings[0]).Contains("data loss");
    }
}
