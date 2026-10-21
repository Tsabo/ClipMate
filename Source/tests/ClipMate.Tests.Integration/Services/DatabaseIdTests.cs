using ClipMate.Data;
using ClipMate.Data.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Integration tests for the permanent Database ID stored inside every database file.
/// </summary>
public class DatabaseIdTests
{
    private string _directory = null!;

    [Before(Test)]
    public void Setup()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"dbid_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
    }

    [After(Test)]
    public void Cleanup()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }
        catch (IOException)
        {
            // Best effort cleanup
        }
    }

    [Test]
    public async Task MigrateAsync_NewDatabase_StoresDatabaseId()
    {
        // Arrange
        var path = Path.Combine(_directory, "new.db");

        // Act
        await CreateAndMigrateAsync(path);

        // Assert
        var databaseId = await ReadDatabaseIdAsync(path);
        await Assert.That(databaseId).IsNotEqualTo(Guid.Empty);
    }

    [Test]
    public async Task MigrateAsync_DatabaseCreatedBeforeDatabaseIdExisted_AssignsDatabaseId()
    {
        // Arrange
        var path = Path.Combine(_directory, "old.db");
        await CreateDatabaseWithoutDatabaseIdAsync(path);

        // Act
        await MigrateAsync(path);

        // Assert
        var databaseId = await ReadDatabaseIdAsync(path);
        await Assert.That(databaseId).IsNotEqualTo(Guid.Empty);
    }

    [Test]
    public async Task MigrateAsync_RunAgain_KeepsSameDatabaseId()
    {
        // Arrange
        var path = Path.Combine(_directory, "stable.db");
        await CreateAndMigrateAsync(path);
        var originalId = await ReadDatabaseIdAsync(path);

        // Act
        await MigrateAsync(path);

        // Assert
        await Assert.That(await ReadDatabaseIdAsync(path)).IsEqualTo(originalId);
    }

    [Test]
    public async Task MigrateAsync_CopiedDatabaseFile_KeepsSameDatabaseId()
    {
        // Arrange
        var path = Path.Combine(_directory, "original.db");
        var copyPath = Path.Combine(_directory, "copy.db");
        await CreateAndMigrateAsync(path);
        var originalId = await ReadDatabaseIdAsync(path);
        SqliteConnection.ClearAllPools();
        File.Copy(path, copyPath);

        // Act
        await MigrateAsync(copyPath);

        // Assert
        await Assert.That(await ReadDatabaseIdAsync(copyPath)).IsEqualTo(originalId);
    }

    [Test]
    public async Task MigrateAsync_TwoNewDatabases_GetDifferentDatabaseIds()
    {
        // Arrange
        var firstPath = Path.Combine(_directory, "first.db");
        var secondPath = Path.Combine(_directory, "second.db");

        // Act
        await CreateAndMigrateAsync(firstPath);
        await CreateAndMigrateAsync(secondPath);

        // Assert
        await Assert.That(await ReadDatabaseIdAsync(firstPath)).IsNotEqualTo(await ReadDatabaseIdAsync(secondPath));
    }

    /// <summary>
    /// Creates a database file as it looked before the Database ID was introduced.
    /// </summary>
    internal static async Task CreateDatabaseWithoutDatabaseIdAsync(string path)
    {
        await using var context = CreateContext(path);
        await context.Database.EnsureCreatedAsync();
        await context.Database.ExecuteSqlRawAsync("DROP TABLE DatabaseInfo");
    }

    private static async Task MigrateAsync(string path)
    {
        await using var context = CreateContext(path);
        await new DatabaseSchemaMigrationService().MigrateAsync(context);
    }

    private static ClipMateDbContext CreateContext(string path)
    {
        var options = new DbContextOptionsBuilder<ClipMateDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;

        return new ClipMateDbContext(options);
    }

    private static async Task CreateAndMigrateAsync(string path)
    {
        await using var context = CreateContext(path);
        await context.Database.EnsureCreatedAsync();
        await new DatabaseSchemaMigrationService().MigrateAsync(context);
    }

    private static async Task<Guid> ReadDatabaseIdAsync(string path)
    {
        await using var context = CreateContext(path);
        var info = await context.DatabaseInfo.SingleAsync();

        return info.DatabaseId;
    }
}
