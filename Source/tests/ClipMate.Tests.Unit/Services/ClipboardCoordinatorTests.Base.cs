using System.Threading.Channels;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data;
using ClipMate.Data.Services;
using ClipMate.Platform;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Base class for ClipboardCoordinator tests containing shared setup and helper methods.
/// </summary>
public partial class ClipboardCoordinatorTests : TestFixtureBase
{
    private IClipboardService CreateMockClipboardService(out Channel<Clip> channel)
    {
        var mock = A.Fake<IClipboardService>();
        channel = Channel.CreateUnbounded<Clip>();

        A.CallTo(() => mock.ClipsChannel).Returns(channel.Reader);
        A.CallTo(() => mock.StartMonitoringAsync(A<CancellationToken>.Ignored)).Returns(Task.CompletedTask);
        A.CallTo(() => mock.StopMonitoringAsync()).Returns(Task.CompletedTask);
        A.CallTo(() => mock.GetCurrentClipboardContentAsync(A<ManualCaptureKind>.Ignored, A<CancellationToken>.Ignored)).Returns((Clip?)null);

        return mock;
    }

    private IConfigurationService CreateMockConfigurationService(bool enableAutoCapture = true)
    {
        var mock = A.Fake<IConfigurationService>();
        var config = new ClipMateConfiguration
        {
            Preferences = new PreferencesConfiguration
            {
                EnableAutoCaptureAtStartup = enableAutoCapture,
                CaptureExistingClipboardAtStartup = false,
            },
        };

        A.CallTo(() => mock.Configuration).Returns(config);
        return mock;
    }

    private IServiceProvider CreateMockServiceProvider(IClipService? clipService = null,
        ICollectionService? collectionService = null,
        IFolderService? folderService = null,
        IApplicationFilterService? filterService = null,
        IDatabaseManager? databaseManager = null,
        IDatabaseContextFactory? contextFactory = null,
        IClipAppendService? clipAppendService = null)
    {
        var clipServiceProvided = clipService != null;
        var collectionServiceProvided = collectionService != null;
        var folderServiceProvided = folderService != null;
        var filterServiceProvided = filterService != null;
        var databaseManagerProvided = databaseManager != null;

        clipService ??= A.Fake<IClipService>();
        collectionService ??= A.Fake<ICollectionService>();
        folderService ??= A.Fake<IFolderService>();
        filterService ??= A.Fake<IApplicationFilterService>();
        databaseManager ??= A.Fake<IDatabaseManager>();
        contextFactory ??= A.Fake<IDatabaseContextFactory>();
        clipAppendService ??= A.Fake<IClipAppendService>();

        // Setup default behaviors only if not provided by caller
        if (!clipServiceProvided)
        {
            A.CallTo(() => clipService.CreateAsync(A<string>.Ignored, A<Clip>.Ignored, A<CancellationToken>.Ignored))
                .ReturnsLazily((string dbKey, Clip clip, CancellationToken ct) => clip);
        }

        if (!collectionServiceProvided)
        {
            A.CallTo(() => collectionService.GetActiveAsync(A<CancellationToken>.Ignored))
                .Returns(new Collection
                {
                    Id = Guid.NewGuid(),
                    ParentId = null,
                    Title = "Inbox",
                    LmType = CollectionLmType.Normal,
                    AcceptNewClips = true,
                });

            // Setup GetActiveDatabaseKey to return a test database key
            A.CallTo(() => collectionService.GetActiveDatabaseKey())
                .Returns("test-database");
        }

        if (!folderServiceProvided)
        {
            A.CallTo(() => folderService.GetActiveAsync(A<CancellationToken>.Ignored))
                .Returns(new Folder
                {
                    Id = Guid.NewGuid(),
                    Name = "Inbox",
                    FolderType = FolderType.Inbox,
                });
        }

        if (!filterServiceProvided)
        {
            A.CallTo(() => filterService.ShouldFilterAsync(A<string?>.Ignored, A<string?>.Ignored, A<CancellationToken>.Ignored))
                .Returns(false);
        }

        if (!databaseManagerProvided)
        {
            // Setup DatabaseManager to return a mock DbContext
            // For unit tests, we'll need to return null or a mock context
            // The actual implementation will handle the null case
            A.CallTo(() => databaseManager.CreateDatabaseContext(A<string>.Ignored))
                .Returns((ClipMateDbContext?)null);
        }

        var services = new ServiceCollection();
        services.AddTransient(_ => clipService);
        services.AddTransient(_ => collectionService);
        services.AddTransient(_ => folderService);
        services.AddTransient(_ => filterService);
        services.AddTransient(_ => databaseManager);
        services.AddTransient(_ => contextFactory);
        services.AddTransient(_ => clipAppendService);
        services.AddTransient(_ => A.Fake<ISoundService>());
        services.AddLogging();

        return services.BuildServiceProvider();
    }

    private ISoundService CreateMockSoundService()
    {
        var mock = A.Fake<ISoundService>();
        A.CallTo(() => mock.PlaySoundAsync(A<SoundEvent>.Ignored, A<CancellationToken>.Ignored)).Returns(Task.CompletedTask);
        return mock;
    }
}
