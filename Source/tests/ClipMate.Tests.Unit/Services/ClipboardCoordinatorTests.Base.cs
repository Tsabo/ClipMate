using System.Threading.Channels;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Data;
using ClipMate.Data.Services;
using ClipMate.Platform;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Base class for ClipboardCoordinator tests containing shared setup and helper methods.
/// </summary>
public partial class ClipboardCoordinatorTests : TestFixtureBase
{
    private Mock<IClipboardService> CreateMockClipboardService(out Channel<Clip> channel)
    {
        var mock = new Mock<IClipboardService>();
        channel = Channel.CreateUnbounded<Clip>();

        mock.Setup(p => p.ClipsChannel).Returns(channel.Reader);
        mock.Setup(p => p.StartMonitoringAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        mock.Setup(p => p.StopMonitoringAsync()).Returns(Task.CompletedTask);
        mock.Setup(p => p.GetCurrentClipboardContentAsync(It.IsAny<CancellationToken>())).ReturnsAsync((Clip?)null);

        return mock;
    }

    private Mock<IConfigurationService> CreateMockConfigurationService(bool enableAutoCapture = true)
    {
        var mock = new Mock<IConfigurationService>();
        var config = new ClipMateConfiguration
        {
            Preferences = new PreferencesConfiguration
            {
                EnableAutoCaptureAtStartup = enableAutoCapture,
                CaptureExistingClipboardAtStartup = false,
            },
        };

        mock.Setup(p => p.Configuration).Returns(config);
        return mock;
    }

    private IServiceProvider CreateMockServiceProvider(Mock<IClipService>? clipService = null,
        Mock<ICollectionService>? collectionService = null,
        Mock<IFolderService>? folderService = null,
        Mock<IApplicationFilterService>? filterService = null,
        Mock<IDatabaseManager>? databaseManager = null,
        Mock<IDatabaseContextFactory>? contextFactory = null)
    {
        var clipServiceProvided = clipService != null;
        var collectionServiceProvided = collectionService != null;
        var folderServiceProvided = folderService != null;
        var filterServiceProvided = filterService != null;
        var databaseManagerProvided = databaseManager != null;

        clipService ??= new Mock<IClipService>();
        collectionService ??= new Mock<ICollectionService>();
        folderService ??= new Mock<IFolderService>();
        filterService ??= new Mock<IApplicationFilterService>();
        databaseManager ??= new Mock<IDatabaseManager>();
        contextFactory ??= new Mock<IDatabaseContextFactory>();

        // Setup default behaviors only if not provided by caller
        if (!clipServiceProvided)
        {
            clipService.Setup(p => p.CreateAsync(It.IsAny<string>(), It.IsAny<Clip>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string dbKey, Clip clip, CancellationToken ct) => clip);
        }

        if (!collectionServiceProvided)
        {
            collectionService.Setup(p => p.GetActiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Collection
                {
                    Id = Guid.NewGuid(),
                    ParentId = null,
                    Title = "Inbox",
                    LmType = CollectionLmType.Normal,
                });

            // Setup GetActiveDatabaseKey to return a test database key
            collectionService.Setup(p => p.GetActiveDatabaseKey())
                .Returns("test-database");
        }

        if (!folderServiceProvided)
        {
            folderService.Setup(p => p.GetActiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Folder
                {
                    Id = Guid.NewGuid(),
                    Name = "Inbox",
                    FolderType = FolderType.Inbox,
                });
        }

        if (!filterServiceProvided)
        {
            filterService.Setup(p => p.ShouldFilterAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        }

        if (!databaseManagerProvided)
        {
            // Setup DatabaseManager to return a mock DbContext
            // For unit tests, we'll need to return null or a mock context
            // The actual implementation will handle the null case
            databaseManager.Setup(p => p.CreateDatabaseContext(It.IsAny<string>()))
                .Returns((ClipMateDbContext?)null);
        }

        var services = new ServiceCollection();
        services.AddTransient(_ => clipService.Object);
        services.AddTransient(_ => collectionService.Object);
        services.AddTransient(_ => folderService.Object);
        services.AddTransient(_ => filterService.Object);
        services.AddTransient(_ => databaseManager.Object);
        services.AddTransient(_ => contextFactory.Object);
        services.AddTransient(_ => new Mock<ISoundService>().Object);
        services.AddLogging();

        return services.BuildServiceProvider();
    }

    private Mock<ISoundService> CreateMockSoundService()
    {
        var mock = new Mock<ISoundService>();
        mock.Setup(p => p.PlaySoundAsync(It.IsAny<SoundEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return mock;
    }
}
