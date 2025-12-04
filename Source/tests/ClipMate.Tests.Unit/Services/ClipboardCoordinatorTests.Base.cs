using System.Threading.Channels;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
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
        Mock<IApplicationFilterService>? filterService = null)
    {
        var clipServiceProvided = clipService != null;
        var collectionServiceProvided = collectionService != null;
        var folderServiceProvided = folderService != null;
        var filterServiceProvided = filterService != null;

        clipService ??= new Mock<IClipService>();
        collectionService ??= new Mock<ICollectionService>();
        folderService ??= new Mock<IFolderService>();
        filterService ??= new Mock<IApplicationFilterService>();

        // Setup default behaviors only if not provided by caller
        if (!clipServiceProvided)
        {
            clipService.Setup(p => p.CreateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Clip clip, CancellationToken ct) => clip);
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

        var services = new ServiceCollection();
        services.AddScoped(_ => clipService.Object);
        services.AddScoped(_ => collectionService.Object);
        services.AddScoped(_ => folderService.Object);
        services.AddScoped(_ => filterService.Object);

        return services.BuildServiceProvider();
    }

    private Mock<ISoundService> CreateMockSoundService()
    {
        var mock = new Mock<ISoundService>();
        mock.Setup(p => p.PlaySoundAsync(It.IsAny<SoundEvent>())).Returns(Task.CompletedTask);
        return mock;
    }
}
