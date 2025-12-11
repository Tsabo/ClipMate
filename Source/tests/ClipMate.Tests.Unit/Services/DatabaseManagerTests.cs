using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Data;
using ClipMate.Data.Services;
using Microsoft.Extensions.Logging;
using Moq;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace ClipMate.Tests.Unit.Services;

public class DatabaseManagerTests
{
    // Constructor Tests
    [Test]
    public async Task Constructor_WithNullConfigService_ThrowsArgumentNullException()
    {
        // Arrange
        var contextFactory = new Mock<IDatabaseContextFactory>();
        var logger = new Mock<ILogger<DatabaseManager>>();

        // Act & Assert
        await Assert.That(() => new DatabaseManager(null!, contextFactory.Object, logger.Object))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithNullContextFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var configService = new Mock<IConfigurationService>();
        var logger = new Mock<ILogger<DatabaseManager>>();

        // Act & Assert
        await Assert.That(() => new DatabaseManager(configService.Object, null!, logger.Object))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var configService = new Mock<IConfigurationService>();
        var contextFactory = new Mock<IDatabaseContextFactory>();

        // Act & Assert
        await Assert.That(() => new DatabaseManager(configService.Object, contextFactory.Object, null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var configService = new Mock<IConfigurationService>();
        var contextFactory = new Mock<IDatabaseContextFactory>();
        var logger = new Mock<ILogger<DatabaseManager>>();

        // Act
        var manager = new DatabaseManager(configService.Object, contextFactory.Object, logger.Object);

        // Assert
        await Assert.That(manager).IsNotNull();
    }

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
                { "test", new DatabaseConfiguration { Name = "Test DB", Directory = "test.db", AutoLoad = false } }
            }
        };

        configService.Setup(c => c.LoadAsync(It.IsAny<CancellationToken>()))
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
            Databases = new Dictionary<string, DatabaseConfiguration>()
        };

        configService.Setup(c => c.LoadAsync(It.IsAny<CancellationToken>()))
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
            Databases = new Dictionary<string, DatabaseConfiguration>()
        };

        configService.Setup(c => c.LoadAsync(It.IsAny<CancellationToken>()))
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
        contextFactory.Verify(f => f.Dispose(), Times.Once);
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
        contextFactory.Verify(f => f.Dispose(), Times.Once);
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
}
