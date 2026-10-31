using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data;
using ClipMate.Data.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

public class DatabaseManagerTests
{
    // LoadAutoLoadDatabasesAsync Tests
    [Test]
    public async Task LoadAutoLoadDatabasesAsync_WithNoAutoLoadDatabases_ReturnsZero()
    {
        // Arrange
        var configService = A.Fake<IConfigurationService>();
        var contextFactory = A.Fake<IDatabaseContextFactory>();
        var logger = A.Fake<ILogger<DatabaseManager>>();

        var config = new ClipMateConfiguration
        {
            Databases = new Dictionary<string, DatabaseConfiguration>
            {
                { "test", new DatabaseConfiguration { Name = "Test DB", FilePath = "test.db", AutoLoad = false } },
            },
        };

        A.CallTo(() => configService.LoadAsync(A<CancellationToken>.Ignored))
            .Returns(config);

        var messenger = A.Fake<IMessenger>();
        var manager = new DatabaseManager(configService, contextFactory, logger, messenger, new DatabaseSchemaMigrationService());

        // Act
        var count = await manager.LoadAutoLoadDatabasesAsync();

        // Assert
        await Assert.That(count).IsEqualTo(0);
    }

    // LoadDatabaseAsync Tests
    [Test]
    public async Task LoadDatabaseAsync_WithNonExistentDatabase_ReturnsFalse()
    {
        // Arrange
        var configService = A.Fake<IConfigurationService>();
        var contextFactory = A.Fake<IDatabaseContextFactory>();
        var logger = A.Fake<ILogger<DatabaseManager>>();

        var config = new ClipMateConfiguration
        {
            Databases = new Dictionary<string, DatabaseConfiguration>(),
        };

        A.CallTo(() => configService.LoadAsync(A<CancellationToken>.Ignored))
            .Returns(config);

        var messenger = A.Fake<IMessenger>();
        var manager = new DatabaseManager(configService, contextFactory, logger, messenger, new DatabaseSchemaMigrationService());

        // Act
        var result = await manager.LoadDatabaseAsync("NonExistent");

        // Assert
        await Assert.That(result).IsFalse();
    }

    // UnloadDatabase Tests
    [Test]
    public async Task UnloadDatabase_WithoutLoadedConfiguration_ReturnsFalse()
    {
        // Arrange
        var configService = A.Fake<IConfigurationService>();
        var contextFactory = A.Fake<IDatabaseContextFactory>();
        var logger = A.Fake<ILogger<DatabaseManager>>();

        var messenger = A.Fake<IMessenger>();
        var manager = new DatabaseManager(configService, contextFactory, logger, messenger, new DatabaseSchemaMigrationService());

        // Act
        var result = manager.UnloadDatabase("Test DB");

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task UnloadDatabase_WithNonExistentDatabase_ReturnsFalse()
    {
        // Arrange
        var configService = A.Fake<IConfigurationService>();
        var contextFactory = A.Fake<IDatabaseContextFactory>();
        var logger = A.Fake<ILogger<DatabaseManager>>();

        var config = new ClipMateConfiguration
        {
            Databases = new Dictionary<string, DatabaseConfiguration>(),
        };

        A.CallTo(() => configService.LoadAsync(A<CancellationToken>.Ignored))
            .Returns(config);

        var messenger = A.Fake<IMessenger>();
        var manager = new DatabaseManager(configService, contextFactory, logger, messenger, new DatabaseSchemaMigrationService());
        await manager.LoadAutoLoadDatabasesAsync(); // Load configuration

        // Act
        var result = manager.UnloadDatabase("NonExistent");

        // Assert
        await Assert.That(result).IsFalse();
    }

    // GetLoadedDatabases Tests
    [Test]
    public async Task GetLoadedDatabases_WithoutLoadedConfiguration_ReturnsEmpty()
    {
        // Arrange
        var configService = A.Fake<IConfigurationService>();
        var contextFactory = A.Fake<IDatabaseContextFactory>();
        var logger = A.Fake<ILogger<DatabaseManager>>();

        var messenger = A.Fake<IMessenger>();
        var manager = new DatabaseManager(configService, contextFactory, logger, messenger, new DatabaseSchemaMigrationService());

        // Act
        var databases = manager.GetLoadedDatabases();

        // Assert
        await Assert.That(databases.Any()).IsFalse();
    }

    // Dispose Tests
    [Test]
    public async Task Dispose_CalledOnce_DisposesContextFactory()
    {
        // Arrange
        var configService = A.Fake<IConfigurationService>();
        var contextFactory = A.Fake<IDatabaseContextFactory>();
        var logger = A.Fake<ILogger<DatabaseManager>>();

        var messenger = A.Fake<IMessenger>();
        var manager = new DatabaseManager(configService, contextFactory, logger, messenger, new DatabaseSchemaMigrationService());

        // Act
        manager.Dispose();

        // Assert
        A.CallTo(() => contextFactory.Dispose()).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task Dispose_CalledMultipleTimes_DisposesOnlyOnce()
    {
        // Arrange
        var configService = A.Fake<IConfigurationService>();
        var contextFactory = A.Fake<IDatabaseContextFactory>();
        var logger = A.Fake<ILogger<DatabaseManager>>();

        var messenger = A.Fake<IMessenger>();
        var manager = new DatabaseManager(configService, contextFactory, logger, messenger, new DatabaseSchemaMigrationService());

        // Act
        manager.Dispose();
        manager.Dispose();
        manager.Dispose();

        // Assert
        A.CallTo(() => contextFactory.Dispose()).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task AfterDispose_LoadAutoLoadDatabasesAsync_ThrowsObjectDisposedException()
    {
        // Arrange
        var configService = A.Fake<IConfigurationService>();
        var contextFactory = A.Fake<IDatabaseContextFactory>();
        var logger = A.Fake<ILogger<DatabaseManager>>();

        var messenger = A.Fake<IMessenger>();
        var manager = new DatabaseManager(configService, contextFactory, logger, messenger, new DatabaseSchemaMigrationService());
        manager.Dispose();

        // Act & Assert
        await Assert.That(async () => await manager.LoadAutoLoadDatabasesAsync())
            .Throws<ObjectDisposedException>();
    }

    // CreateAllDatabaseContexts Tests
    [Test]
    public async Task CreateAllDatabaseContexts_WithoutLoadedConfiguration_ReturnsEmpty()
    {
        // Arrange
        var configService = A.Fake<IConfigurationService>();
        var contextFactory = A.Fake<IDatabaseContextFactory>();
        var logger = A.Fake<ILogger<DatabaseManager>>();

        var messenger = A.Fake<IMessenger>();
        var manager = new DatabaseManager(configService, contextFactory, logger, messenger, new DatabaseSchemaMigrationService());

        // Act
        var contexts = manager.CreateAllDatabaseContexts().ToList();

        // Assert
        await Assert.That(contexts).IsEmpty();
    }

    [Test]
    public async Task CreateAllDatabaseContexts_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var configService = A.Fake<IConfigurationService>();
        var contextFactory = A.Fake<IDatabaseContextFactory>();
        var logger = A.Fake<ILogger<DatabaseManager>>();

        var messenger = A.Fake<IMessenger>();
        var manager = new DatabaseManager(configService, contextFactory, logger, messenger, new DatabaseSchemaMigrationService());
        manager.Dispose();

        // Act & Assert
        await Assert.That(() => manager.CreateAllDatabaseContexts().ToList())
            .Throws<ObjectDisposedException>();
    }
}
