using ClipMate.Core.Models;
using ClipMate.Core.Services;
using ClipMate.Data.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Threading.Channels;

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
        
        mock.Setup(s => s.ClipsChannel).Returns(channel.Reader);
        mock.Setup(s => s.StartMonitoringAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        mock.Setup(s => s.StopMonitoringAsync()).Returns(Task.CompletedTask);
        
        return mock;
    }

    private IServiceProvider CreateMockServiceProvider(
        Mock<IClipService>? clipService = null,
        Mock<ICollectionService>? collectionService = null,
        Mock<IFolderService>? folderService = null,
        Mock<IApplicationFilterService>? filterService = null)
    {
        clipService ??= new Mock<IClipService>();
        collectionService ??= new Mock<ICollectionService>();
        folderService ??= new Mock<IFolderService>();
        filterService ??= new Mock<IApplicationFilterService>();

        // Setup default behaviors
        clipService.Setup(s => s.CreateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Clip clip, CancellationToken ct) => clip);

        collectionService.Setup(s => s.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Collection
            {
                Id = Guid.NewGuid(),
                Title = "Default Collection",
                LmType = 0 // Normal collection
            });

        folderService.Setup(s => s.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Folder
            {
                Id = Guid.NewGuid(),
                Name = "Inbox",
                FolderType = FolderType.Inbox
            });

        filterService.Setup(s => s.ShouldFilterAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var services = new ServiceCollection();
        services.AddScoped(_ => clipService.Object);
        services.AddScoped(_ => collectionService.Object);
        services.AddScoped(_ => folderService.Object);
        services.AddScoped(_ => filterService.Object);
        
        return services.BuildServiceProvider();
    }
}
