using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data.Services;
using Microsoft.Extensions.Logging;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

public class DefaultDataInitializationServiceTests : TestFixtureBase
{
    private const string _testDatabaseKey = "test-db";
    private const string _databasePath = ":memory:";
    private readonly ILogger<DefaultDataInitializationService> _logger;
    private readonly ICollectionService _mockCollectionService;
    private readonly IConfigurationService _mockConfigurationService;
    private readonly IServiceProvider _mockServiceProvider;

    public DefaultDataInitializationServiceTests()
    {
        _mockCollectionService = A.Fake<ICollectionService>();
        _mockConfigurationService = A.Fake<IConfigurationService>();
        _mockServiceProvider = A.Fake<IServiceProvider>();
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

        A.CallTo(() => _mockConfigurationService.Configuration).Returns(config);

        // Services are singletons - resolve directly from service provider (no scopes)
        A.CallTo(() => _mockServiceProvider.GetService(typeof(ICollectionService))).Returns(_mockCollectionService);
        A.CallTo(() => _mockServiceProvider.GetService(typeof(IConfigurationService))).Returns(_mockConfigurationService);
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

        A.CallTo(() => _mockCollectionService.GetAllByDatabaseKeyAsync(_databasePath, A<CancellationToken>.Ignored))
            .Returns([inboxCollection]);

        var service = new DefaultDataInitializationService(_mockServiceProvider, _logger);

        // Act
        await service.InitializeAsync();

        // Assert - database path is used as the key for SetActiveAsync
        A.CallTo(() => _mockCollectionService.SetActiveAsync(inboxCollection.Id, _databasePath, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
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

        A.CallTo(() => _mockCollectionService.GetAllByDatabaseKeyAsync(_databasePath, A<CancellationToken>.Ignored))
            .Returns([otherCollection]);

        var service = new DefaultDataInitializationService(_mockServiceProvider, _logger);

        // Act
        await service.InitializeAsync();

        // Assert - Service should log error and return without setting active
        // Database seeding (DatabaseSchemaInitializationStep) is responsible for creating Inbox
        A.CallTo(() => _mockCollectionService.SetActiveAsync(
            A<Guid>.Ignored,
            A<string>.Ignored,
            A<CancellationToken>.Ignored)).MustNotHaveHappened();
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

        A.CallTo(() => _mockCollectionService.GetAllByDatabaseKeyAsync(_databasePath, A<CancellationToken>.Ignored))
            .Returns(collections);

        var service = new DefaultDataInitializationService(_mockServiceProvider, _logger);

        // Act
        await service.InitializeAsync();

        // Assert - should set the Inbox collection as active using database path
        var inboxId = collections[1].Id;
        A.CallTo(() => _mockCollectionService.SetActiveAsync(inboxId, _databasePath, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
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

        A.CallTo(() => _mockCollectionService.GetAllByDatabaseKeyAsync(_databasePath, A<CancellationToken>.Ignored))
            .Returns([inboxCollection]);

        var service = new DefaultDataInitializationService(_mockServiceProvider, _logger);

        // Act
        await service.InitializeAsync();

        // Assert - should still find and set it as active using database path
        A.CallTo(() => _mockCollectionService.SetActiveAsync(inboxCollection.Id, _databasePath, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }
}
