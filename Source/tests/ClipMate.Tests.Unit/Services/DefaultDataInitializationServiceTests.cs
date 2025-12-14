using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Data.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

public class DefaultDataInitializationServiceTests : TestFixtureBase
{
    private const string _testDatabaseKey = "test-db";
    private readonly ILogger<DefaultDataInitializationService> _logger;
    private readonly Mock<ICollectionService> _mockCollectionService;
    private readonly Mock<IConfigurationService> _mockConfigurationService;
    private readonly Mock<IServiceProvider> _mockScopedServiceProvider;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IServiceScope> _mockServiceScope;
    private readonly Mock<IServiceScopeFactory> _mockServiceScopeFactory;

    public DefaultDataInitializationServiceTests()
    {
        _mockCollectionService = new Mock<ICollectionService>();
        _mockConfigurationService = new Mock<IConfigurationService>();
        _mockScopedServiceProvider = new Mock<IServiceProvider>();
        _mockServiceScope = new Mock<IServiceScope>();
        _mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _logger = CreateLogger<DefaultDataInitializationService>();

        // Setup default configuration with test database
        var config = new ClipMateConfiguration
        {
            DefaultDatabase = _testDatabaseKey,
            Databases = new Dictionary<string, DatabaseConfiguration>
            {
                [_testDatabaseKey] = new()
                    { Name = "Test Database" },
            },
        };

        _mockConfigurationService.Setup(x => x.Configuration).Returns(config);

        // Setup service provider chain for CreateScope pattern
        _mockServiceScope.Setup(p => p.ServiceProvider).Returns(_mockScopedServiceProvider.Object);
        _mockScopedServiceProvider.Setup(p => p.GetService(typeof(ICollectionService))).Returns(_mockCollectionService.Object);
        _mockScopedServiceProvider.Setup(p => p.GetService(typeof(IConfigurationService))).Returns(_mockConfigurationService.Object);
        _mockServiceScopeFactory.Setup(p => p.CreateScope()).Returns(_mockServiceScope.Object);
        _mockServiceProvider.Setup(p => p.GetService(typeof(IServiceScopeFactory))).Returns(_mockServiceScopeFactory.Object);
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

        _mockCollectionService.Setup(p => p.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([inboxCollection]);

        var service = new DefaultDataInitializationService(_mockServiceProvider.Object, _logger);

        // Act
        await service.InitializeAsync();

        // Assert
        _mockCollectionService.Verify(p => p.SetActiveAsync(inboxCollection.Id, _testDatabaseKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task InitializeAsync_WhenInboxDoesNotExist_ShouldCreateAndSetActive()
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

        var newInboxCollection = new Collection
        {
            Id = Guid.NewGuid(),
            Name = "Inbox",
            Description = "Default collection for clipboard captures",
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
        };

        _mockCollectionService.Setup(p => p.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([otherCollection]);

        _mockCollectionService.Setup(p => p.CreateAsync(
                "Inbox",
                "Default collection for clipboard captures",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(newInboxCollection);

        var service = new DefaultDataInitializationService(_mockServiceProvider.Object, _logger);

        // Act
        await service.InitializeAsync();

        // Assert
        _mockCollectionService.Verify(p => p.CreateAsync(
            "Inbox",
            "Default collection for clipboard captures",
            It.IsAny<CancellationToken>()), Times.Once);

        _mockCollectionService.Verify(p => p.SetActiveAsync(newInboxCollection.Id, _testDatabaseKey, It.IsAny<CancellationToken>()), Times.Once);
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

        _mockCollectionService.Setup(p => p.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(collections);

        var service = new DefaultDataInitializationService(_mockServiceProvider.Object, _logger);

        // Act
        await service.InitializeAsync();

        // Assert - should set the Inbox collection as active
        var inboxId = collections[1].Id;
        _mockCollectionService.Verify(p => p.SetActiveAsync(inboxId, _testDatabaseKey, It.IsAny<CancellationToken>()), Times.Once);
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

        _mockCollectionService.Setup(p => p.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([inboxCollection]);

        var service = new DefaultDataInitializationService(_mockServiceProvider.Object, _logger);

        // Act
        await service.InitializeAsync();

        // Assert - should still find and set it as active
        _mockCollectionService.Verify(p => p.SetActiveAsync(inboxCollection.Id, _testDatabaseKey, It.IsAny<CancellationToken>()), Times.Once);
        _mockCollectionService.Verify(p => p.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
