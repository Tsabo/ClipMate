using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Data;
using ClipMate.Data.Services;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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

    [Test]
    public async Task LoadDatabaseAsync_DatabaseCreatedBeforeDatabaseIdExisted_AssignsDatabaseId()
    {
        // Arrange
        var path = Path.Combine(_directory, "old.db");
        await CreateDatabaseWithoutDatabaseIdAsync(path);
        var configService = CreateConfigurationService(("db_old", path, false));
        using var manager = CreateDatabaseManager(configService);

        // Act
        var loaded = await manager.LoadDatabaseAsync("db_old");

        // Assert
        await Assert.That(loaded).IsTrue();
        await Assert.That(await new DatabaseIdService(configService).GetDatabaseIdAsync("db_old")).IsNotNull();
    }

    [Test]
    public async Task LoadAutoLoadDatabasesAsync_DatabaseCreatedBeforeDatabaseIdExisted_AssignsDatabaseId()
    {
        // Arrange
        var path = Path.Combine(_directory, "old.db");
        await CreateDatabaseWithoutDatabaseIdAsync(path);
        var configService = CreateConfigurationService(("db_old", path, true));
        using var manager = CreateDatabaseManager(configService);

        // Act
        await manager.LoadAutoLoadDatabasesAsync();

        // Assert
        await Assert.That(await new DatabaseIdService(configService).GetDatabaseIdAsync("db_old")).IsNotNull();
    }

    [Test]
    public async Task GetDatabaseIdAsync_ReturnsIdStoredInFile()
    {
        // Arrange
        var path = Path.Combine(_directory, "stored.db");
        await CreateAndMigrateAsync(path);
        var service = new DatabaseIdService(CreateConfigurationService(("db_stored", path, true)));

        // Act
        var databaseId = await service.GetDatabaseIdAsync("db_stored");

        // Assert
        await Assert.That(databaseId).IsEqualTo(await ReadDatabaseIdAsync(path));
    }

    [Test]
    public async Task GetDatabaseIdAsync_AfterRenamingDatabase_ReturnsSameId()
    {
        // Arrange
        var path = Path.Combine(_directory, "renamed.db");
        await CreateAndMigrateAsync(path);
        var configService = CreateConfigurationService(("db_renamed", path, true));
        var service = new DatabaseIdService(configService);
        var originalId = await service.GetDatabaseIdAsync("db_renamed");

        // Act
        configService.Configuration.Databases["db_renamed"].Name = "Renamed Database";
        var databaseId = await service.GetDatabaseIdAsync("db_renamed");

        // Assert
        await Assert.That(databaseId).IsEqualTo(originalId);
    }

    [Test]
    public async Task GetDatabaseIdAsync_FileDoesNotExist_ReturnsNullWithoutCreatingFile()
    {
        // Arrange
        var path = Path.Combine(_directory, "missing.db");
        var service = new DatabaseIdService(CreateConfigurationService(("db_missing", path, true)));

        // Act
        var databaseId = await service.GetDatabaseIdAsync("db_missing");

        // Assert
        await Assert.That(databaseId).IsNull();
        await Assert.That(File.Exists(path)).IsFalse();
    }

    [Test]
    public async Task GetDatabaseIdAsync_DatabaseNotYetUpgraded_ReturnsNull()
    {
        // Arrange
        var path = Path.Combine(_directory, "old.db");
        await CreateDatabaseWithoutDatabaseIdAsync(path);
        var service = new DatabaseIdService(CreateConfigurationService(("db_old", path, true)));

        // Act
        var databaseId = await service.GetDatabaseIdAsync("db_old");

        // Assert
        await Assert.That(databaseId).IsNull();
    }

    [Test]
    public async Task GetDatabaseIdAsync_UnknownDatabaseKey_ReturnsNull()
    {
        // Arrange
        var service = new DatabaseIdService(CreateConfigurationService());

        // Act
        var databaseId = await service.GetDatabaseIdAsync("db_unknown");

        // Assert
        await Assert.That(databaseId).IsNull();
    }

    private static IConfigurationService CreateConfigurationService(params (string Key, string Path, bool AutoLoad)[] databases)
    {
        var configuration = new ClipMateConfiguration
        {
            Databases = databases.ToDictionary(
                p => p.Key,
                p => new DatabaseConfiguration { Name = p.Key, FilePath = p.Path, AutoLoad = p.AutoLoad }),
        };

        var configService = A.Fake<IConfigurationService>();
        A.CallTo(() => configService.Configuration).Returns(configuration);
        A.CallTo(() => configService.LoadAsync(A<CancellationToken>.Ignored)).Returns(configuration);

        return configService;
    }

    private static DatabaseManager CreateDatabaseManager(IConfigurationService configService)
    {
#pragma warning disable CA2000 // The DatabaseManager disposes the context factory
        var contextFactory = new DatabaseContextFactory(A.Fake<IServiceProvider>(), configService,
            A.Fake<ILogger<DatabaseContextFactory>>());
#pragma warning restore CA2000

        return new DatabaseManager(configService, contextFactory, A.Fake<ILogger<DatabaseManager>>(),
            A.Fake<IMessenger>(), new DatabaseSchemaMigrationService());
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
