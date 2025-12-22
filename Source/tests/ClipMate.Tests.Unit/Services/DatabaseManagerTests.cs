using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Data;
using ClipMate.Data.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

public class DatabaseManagerTests
{
    // LoadAutoLoadDatabasesAsync Tests
    [Test]
    public async Task LoadAutoLoadDatabasesAsync_WithNoAutoLoadDatabases_ReturnsZero()
    {
        // Arrange
        var configService = new Mock<IConfigurationService>();
        var contextFactory = new Mock<IDatabaseContextFactory>();
        var logger = new Mock<ILogger<DatabaseManager>>();

        var config = new ClipMateConfiguration
        {
            Databases = new Dictionary<string, DatabaseConfiguration>
            {
                { "test", new DatabaseConfiguration { Name = "Test DB", FilePath = "test.db", AutoLoad = false } },
            },
        };

        configService.Setup(p => p.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var manager = new DatabaseManager(configService.Object, contextFactory.Object, logger.Object);

        // Act
        var count = await manager.LoadAutoLoadDatabasesAsync();

        // Assert
        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    [Skip("DatabaseManager requires real EF Core DbContext with Database facade - tested in integration tests")]
    public async Task LoadAutoLoadDatabasesAsync_WithAutoLoadDatabases_LoadsThem()
    {
        // This test requires a real DbContext because DatabaseManager calls:
        // - context.Database.EnsureCreatedAsync()
        // The Database property and its methods cannot be easily mocked.
        // See integration tests for coverage of this functionality.
        await Task.CompletedTask;
    }

    // LoadDatabaseAsync Tests
    [Test]
    public async Task LoadDatabaseAsync_WithNonExistentDatabase_ReturnsFalse()
    {
        // Arrange
        var configService = new Mock<IConfigurationService>();
        var contextFactory = new Mock<IDatabaseContextFactory>();
        var logger = new Mock<ILogger<DatabaseManager>>();

        var config = new ClipMateConfiguration
        {
            Databases = new Dictionary<string, DatabaseConfiguration>(),
        };

        configService.Setup(p => p.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var manager = new DatabaseManager(configService.Object, contextFactory.Object, logger.Object);

        // Act
        var result = await manager.LoadDatabaseAsync("NonExistent");

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    [Skip("DatabaseManager requires real EF Core DbContext with Database facade - tested in integration tests")]
    public async Task LoadDatabaseAsync_WithValidDatabase_ReturnsTrue()
    {
        // This test requires a real DbContext because DatabaseManager calls:
        // - context.Database.EnsureCreatedAsync()
        // The Database property and its methods cannot be easily mocked.
        // See integration tests for coverage of this functionality.
        await Task.CompletedTask;
    }

    // UnloadDatabase Tests
    [Test]
    public async Task UnloadDatabase_WithoutLoadedConfiguration_ReturnsFalse()
    {
        // Arrange
        var configService = new Mock<IConfigurationService>();
        var contextFactory = new Mock<IDatabaseContextFactory>();
        var logger = new Mock<ILogger<DatabaseManager>>();

        var manager = new DatabaseManager(configService.Object, contextFactory.Object, logger.Object);

        // Act
        var result = manager.UnloadDatabase("Test DB");

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task UnloadDatabase_WithNonExistentDatabase_ReturnsFalse()
    {
        // Arrange
        var configService = new Mock<IConfigurationService>();
        var contextFactory = new Mock<IDatabaseContextFactory>();
        var logger = new Mock<ILogger<DatabaseManager>>();

        var config = new ClipMateConfiguration
        {
            Databases = new Dictionary<string, DatabaseConfiguration>(),
        };

        configService.Setup(p => p.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var manager = new DatabaseManager(configService.Object, contextFactory.Object, logger.Object);
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
        var configService = new Mock<IConfigurationService>();
        var contextFactory = new Mock<IDatabaseContextFactory>();
        var logger = new Mock<ILogger<DatabaseManager>>();

        var manager = new DatabaseManager(configService.Object, contextFactory.Object, logger.Object);

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
        var configService = new Mock<IConfigurationService>();
        var contextFactory = new Mock<IDatabaseContextFactory>();
        var logger = new Mock<ILogger<DatabaseManager>>();

        var manager = new DatabaseManager(configService.Object, contextFactory.Object, logger.Object);

        // Act
        manager.Dispose();

        // Assert
        contextFactory.Verify(p => p.Dispose(), Times.Once);
    }

    [Test]
    public async Task Dispose_CalledMultipleTimes_DisposesOnlyOnce()
    {
        // Arrange
        var configService = new Mock<IConfigurationService>();
        var contextFactory = new Mock<IDatabaseContextFactory>();
        var logger = new Mock<ILogger<DatabaseManager>>();

        var manager = new DatabaseManager(configService.Object, contextFactory.Object, logger.Object);

        // Act
        manager.Dispose();
        manager.Dispose();
        manager.Dispose();

        // Assert
        contextFactory.Verify(p => p.Dispose(), Times.Once);
    }

    [Test]
    public async Task AfterDispose_LoadAutoLoadDatabasesAsync_ThrowsObjectDisposedException()
    {
        // Arrange
        var configService = new Mock<IConfigurationService>();
        var contextFactory = new Mock<IDatabaseContextFactory>();
        var logger = new Mock<ILogger<DatabaseManager>>();

        var manager = new DatabaseManager(configService.Object, contextFactory.Object, logger.Object);
        manager.Dispose();

        // Act & Assert
        await Assert.That(async () => await manager.LoadAutoLoadDatabasesAsync())
            .Throws<ObjectDisposedException>();
    }

    // GetAllDatabaseContexts Tests
    [Test]
    public async Task GetAllDatabaseContexts_WithoutLoadedConfiguration_ReturnsEmpty()
    {
        // Arrange
        var configService = new Mock<IConfigurationService>();
        var contextFactory = new Mock<IDatabaseContextFactory>();
        var logger = new Mock<ILogger<DatabaseManager>>();

        var manager = new DatabaseManager(configService.Object, contextFactory.Object, logger.Object);

        // Act
        var contexts = manager.GetAllDatabaseContexts().ToList();

        // Assert
        await Assert.That(contexts).IsEmpty();
    }

    [Test]
    [Skip("Requires real DbContext - Mock<ClipMateDbContext> has no parameterless constructor")]
    public async Task GetAllDatabaseContexts_WithLoadedDatabases_ReturnsDatabaseKeys()
    {
        // Arrange
        var configService = new Mock<IConfigurationService>();
        var contextFactory = new Mock<IDatabaseContextFactory>();
        var logger = new Mock<ILogger<DatabaseManager>>();

        var config = new ClipMateConfiguration
        {
            Databases = new Dictionary<string, DatabaseConfiguration>
            {
                { "primary", new DatabaseConfiguration { Name = "My Clips", FilePath = "primary.db", AutoLoad = true } },
                { "secondary", new DatabaseConfiguration { Name = "Secondary", FilePath = "secondary.db", AutoLoad = true } },
            },
        };

        configService.Setup(p => p.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var mockContext1 = new Mock<ClipMateDbContext>();
        var mockContext2 = new Mock<ClipMateDbContext>();

        contextFactory.Setup(p => p.GetLoadedDatabasePaths())
            .Returns(["primary.db", "secondary.db"]);

        contextFactory.Setup(p => p.GetOrCreateContext("primary.db"))
            .Returns(mockContext1.Object);

        contextFactory.Setup(p => p.GetOrCreateContext("secondary.db"))
            .Returns(mockContext2.Object);

        var manager = new DatabaseManager(configService.Object, contextFactory.Object, logger.Object);
        await manager.LoadAutoLoadDatabasesAsync(); // Load configuration

        // Act
        var contexts = manager.GetAllDatabaseContexts().ToList();

        // Assert
        await Assert.That(contexts).Count().IsEqualTo(2);
        await Assert.That(contexts[0].DatabaseKey).IsEqualTo("primary");
        await Assert.That(contexts[1].DatabaseKey).IsEqualTo("secondary");
        await Assert.That(contexts[0].Context).IsEqualTo(mockContext1.Object);
        await Assert.That(contexts[1].Context).IsEqualTo(mockContext2.Object);
    }

    [Test]
    [Skip("Requires real DbContext - Mock<ClipMateDbContext> has no parameterless constructor")]
    public async Task GetAllDatabaseContexts_ReturnsDatabaseKeysNotDisplayNames()
    {
        // Arrange
        var configService = new Mock<IConfigurationService>();
        var contextFactory = new Mock<IDatabaseContextFactory>();
        var logger = new Mock<ILogger<DatabaseManager>>();

        var config = new ClipMateConfiguration
        {
            Databases = new Dictionary<string, DatabaseConfiguration>
            {
                // Key is "test-db", but Name is "Test Database Display Name"
                { "test-db", new DatabaseConfiguration { Name = "Test Database Display Name", FilePath = "test.db", AutoLoad = true } },
            },
        };

        configService.Setup(p => p.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var mockContext = new Mock<ClipMateDbContext>();

        contextFactory.Setup(p => p.GetLoadedDatabasePaths())
            .Returns(["test.db"]);

        contextFactory.Setup(p => p.GetOrCreateContext("test.db"))
            .Returns(mockContext.Object);

        var manager = new DatabaseManager(configService.Object, contextFactory.Object, logger.Object);
        await manager.LoadAutoLoadDatabasesAsync();

        // Act
        var contexts = manager.GetAllDatabaseContexts().ToList();

        // Assert - Should return the dictionary key, not the display name
        await Assert.That(contexts).Count().IsEqualTo(1);
        await Assert.That(contexts[0].DatabaseKey).IsEqualTo("test-db");
        await Assert.That(contexts[0].DatabaseKey).IsNotEqualTo("Test Database Display Name");
    }

    [Test]
    public async Task GetAllDatabaseContexts_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var configService = new Mock<IConfigurationService>();
        var contextFactory = new Mock<IDatabaseContextFactory>();
        var logger = new Mock<ILogger<DatabaseManager>>();

        var manager = new DatabaseManager(configService.Object, contextFactory.Object, logger.Object);
        manager.Dispose();

        // Act & Assert
        await Assert.That(() => manager.GetAllDatabaseContexts().ToList())
            .Throws<ObjectDisposedException>();
    }
}
