using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Data.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

public class DefaultDataInitializationServiceTests : TestFixtureBase
{
    private const string _testDatabaseKey = "test-db";
    private const string _databasePath = ":memory:";
    private readonly ILogger<DefaultDataInitializationService> _logger;
    private readonly Mock<ICollectionService> _mockCollectionService;
    private readonly Mock<IConfigurationService> _mockConfigurationService;
    private readonly Mock<IServiceProvider> _mockServiceProvider;

    public DefaultDataInitializationServiceTests()
    {
        _mockCollectionService = new Mock<ICollectionService>();
        _mockConfigurationService = new Mock<IConfigurationService>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _logger = CreateLogger<DefaultDataInitializationService>();

        // Setup default configuration with test database
        var config = new ClipMateConfiguration
        {
            DefaultDatabase = _testDatabaseKey,
            Databases = new Dictionary<string, DatabaseConfiguration>
            {
                [_testDatabaseKey] = new()
                    { Name = "Test Database", FilePath = _databasePath },
            },
        };

        _mockConfigurationService.Setup(p => p.Configuration).Returns(config);

        // Services are singletons - resolve directly from service provider (no scopes)
        _mockServiceProvider.Setup(p => p.GetService(typeof(ICollectionService))).Returns(_mockCollectionService.Object);
        _mockServiceProvider.Setup(p => p.GetService(typeof(IConfigurationService))).Returns(_mockConfigurationService.Object);
    }

    [Test]
    public async Task InitializeAsync_WhenInboxExists_ShouldSetItAsActive()
    {
        // Arrange
        var inboxCollection = new Collection
        {
            Id = Guid.NewGuid(),
            Name = "Inbox",
            Description = "Default collection",
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
        };

        _mockCollectionService.Setup(p => p.GetAllByDatabaseKeyAsync(_databasePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync([inboxCollection]);

        var service = new DefaultDataInitializationService(_mockServiceProvider.Object, _logger);

        // Act
        await service.InitializeAsync();

        // Assert - database path is used as the key for SetActiveAsync
        _mockCollectionService.Verify(p => p.SetActiveAsync(inboxCollection.Id, _databasePath, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task InitializeAsync_WhenInboxDoesNotExist_ShouldLogErrorAndNotSetActive()
    {
        // Arrange
        var otherCollection = new Collection
        {
            Id = Guid.NewGuid(),
            Name = "Other",
            Description = "Some other collection",
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
        };

        _mockCollectionService.Setup(p => p.GetAllByDatabaseKeyAsync(_databasePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync([otherCollection]);

        var service = new DefaultDataInitializationService(_mockServiceProvider.Object, _logger);

        // Act
        await service.InitializeAsync();

        // Assert - Service should log error and return without setting active
        // Database seeding (DatabaseSchemaInitializationStep) is responsible for creating Inbox
        _mockCollectionService.Verify(p => p.SetActiveAsync(
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task InitializeAsync_WhenMultipleCollectionsExist_ShouldSetInboxAsActive()
    {
        // Arrange
        var collections = new[]
        {
            new Collection
            {
                Id = Guid.NewGuid(),
                Name = "Safe",
                Description = "Safe collection",
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow,
            },
            new Collection
            {
                Id = Guid.NewGuid(),
                Name = "Inbox",
                Description = "Default collection",
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow,
            },
            new Collection
            {
                Id = Guid.NewGuid(),
                Name = "Custom",
                Description = "Custom collection",
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow,
            },
        };

        _mockCollectionService.Setup(p => p.GetAllByDatabaseKeyAsync(_databasePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(collections);

        var service = new DefaultDataInitializationService(_mockServiceProvider.Object, _logger);

        // Act
        await service.InitializeAsync();

        // Assert - should set the Inbox collection as active using database path
        var inboxId = collections[1].Id;
        _mockCollectionService.Verify(p => p.SetActiveAsync(inboxId, _databasePath, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task InitializeAsync_IsCaseInsensitive_ForInboxName()
    {
        // Arrange
        var inboxCollection = new Collection
        {
            Id = Guid.NewGuid(),
            Name = "INBOX", // Different case
            Description = "Default collection",
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
        };

        _mockCollectionService.Setup(p => p.GetAllByDatabaseKeyAsync(_databasePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync([inboxCollection]);

        var service = new DefaultDataInitializationService(_mockServiceProvider.Object, _logger);

        // Act
        await service.InitializeAsync();

        // Assert - should still find and set it as active using database path
        _mockCollectionService.Verify(p => p.SetActiveAsync(inboxCollection.Id, _databasePath, It.IsAny<CancellationToken>()), Times.Once);
    }
}
