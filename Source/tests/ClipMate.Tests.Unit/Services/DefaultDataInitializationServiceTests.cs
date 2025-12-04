using ClipMate.Core.Models;
using ClipMate.Core.Services;
using ClipMate.Data.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

public class DefaultDataInitializationServiceTests : TestFixtureBase
{
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IServiceScopeFactory> _mockServiceScopeFactory;
    private readonly Mock<IServiceScope> _mockServiceScope;
    private readonly Mock<IServiceProvider> _mockScopedServiceProvider;
    private readonly Mock<ICollectionService> _mockCollectionService;
    private readonly ILogger<DefaultDataInitializationService> _logger;

    public DefaultDataInitializationServiceTests()
    {
        _mockCollectionService = new Mock<ICollectionService>();
        _mockScopedServiceProvider = new Mock<IServiceProvider>();
        _mockServiceScope = new Mock<IServiceScope>();
        _mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _logger = CreateLogger<DefaultDataInitializationService>();

        // Setup service provider chain for CreateScope pattern
        _mockServiceScope.Setup(x => x.ServiceProvider).Returns(_mockScopedServiceProvider.Object);
        _mockScopedServiceProvider.Setup(x => x.GetService(typeof(ICollectionService))).Returns(_mockCollectionService.Object);
        _mockServiceScopeFactory.Setup(x => x.CreateScope()).Returns(_mockServiceScope.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(IServiceScopeFactory))).Returns(_mockServiceScopeFactory.Object);
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
            ModifiedAt = DateTime.UtcNow
        };

        _mockCollectionService.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([inboxCollection]);

        var service = new DefaultDataInitializationService(_mockServiceProvider.Object, _logger);

        // Act
        await service.InitializeAsync();

        // Assert
        _mockCollectionService.Verify(x => x.SetActiveAsync(inboxCollection.Id, It.IsAny<CancellationToken>()), Times.Once);
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
            ModifiedAt = DateTime.UtcNow
        };

        var newInboxCollection = new Collection
        {
            Id = Guid.NewGuid(),
            Name = "Inbox",
            Description = "Default collection for clipboard captures",
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };

        _mockCollectionService.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([otherCollection]);

        _mockCollectionService.Setup(x => x.CreateAsync(
                "Inbox",
                "Default collection for clipboard captures",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(newInboxCollection);

        var service = new DefaultDataInitializationService(_mockServiceProvider.Object, _logger);

        // Act
        await service.InitializeAsync();

        // Assert
        _mockCollectionService.Verify(x => x.CreateAsync(
            "Inbox",
            "Default collection for clipboard captures",
            It.IsAny<CancellationToken>()), Times.Once);

        _mockCollectionService.Verify(x => x.SetActiveAsync(newInboxCollection.Id, It.IsAny<CancellationToken>()), Times.Once);
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
                ModifiedAt = DateTime.UtcNow
            },
            new Collection
            {
                Id = Guid.NewGuid(),
                Name = "Inbox",
                Description = "Default collection",
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow
            },
            new Collection
            {
                Id = Guid.NewGuid(),
                Name = "Custom",
                Description = "Custom collection",
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow
            }
        };

        _mockCollectionService.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(collections);

        var service = new DefaultDataInitializationService(_mockServiceProvider.Object, _logger);

        // Act
        await service.InitializeAsync();

        // Assert - should set the Inbox collection as active
        var inboxId = collections[1].Id;
        _mockCollectionService.Verify(x => x.SetActiveAsync(inboxId, It.IsAny<CancellationToken>()), Times.Once);
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
            ModifiedAt = DateTime.UtcNow
        };

        _mockCollectionService.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([inboxCollection]);

        var service = new DefaultDataInitializationService(_mockServiceProvider.Object, _logger);

        // Act
        await service.InitializeAsync();

        // Assert - should still find and set it as active
        _mockCollectionService.Verify(x => x.SetActiveAsync(inboxCollection.Id, It.IsAny<CancellationToken>()), Times.Once);
        _mockCollectionService.Verify(x => x.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

}

