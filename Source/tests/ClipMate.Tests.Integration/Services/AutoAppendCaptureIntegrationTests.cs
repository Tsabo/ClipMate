using System.Threading.Channels;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using ClipMate.Data;
using ClipMate.Data.Repositories;
using ClipMate.Data.Services;
using ClipMate.Platform;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Auto-Append through the capture pipeline: captures go in through the clipboard service's channel, and the clips in a
/// real database come out.
/// </summary>
public class AutoAppendCaptureIntegrationTests : IntegrationTestBase
{
    private const string _databaseKey = "db_autoappend";

    private readonly AutoAppendService _autoAppendService = new();
    private Channel<Clip> _channel = null!;
    private ClipRepository _clipRepository = null!;
    private ClipboardCoordinator _coordinator = null!;
    private ServiceProvider _serviceProvider = null!;

    [Before(Test)]
    public async Task SetupCoordinatorAsync()
    {
        var collection = new Collection
        {
            Id = Guid.NewGuid(),
            Name = "Inbox",
            CreatedAt = DateTime.UtcNow,
            AcceptNewClips = true,
        };

        DbContext.Collections.Add(collection);
        await DbContext.SaveChangesAsync();

        _clipRepository = new ClipRepository(DbContext, A.Fake<ILogger<ClipRepository>>());

        var contextFactory = A.Fake<IDatabaseContextFactory>();
        A.CallTo(() => contextFactory.GetClipRepository(_databaseKey)).Returns(_clipRepository);
        A.CallTo(() => contextFactory.GetClipDataRepository(_databaseKey)).Returns(new ClipDataRepository(DbContext));
        A.CallTo(() => contextFactory.GetBlobRepository(_databaseKey)).Returns(new BlobRepository(DbContext));

        var collectionService = A.Fake<ICollectionService>();
        A.CallTo(() => collectionService.GetActiveDatabaseKey()).Returns(_databaseKey);
        A.CallTo(() => collectionService.GetActiveAsync(A<CancellationToken>.Ignored)).Returns(collection);

        var folderService = A.Fake<IFolderService>();
        A.CallTo(() => folderService.GetActiveAsync(A<CancellationToken>.Ignored)).Returns((Folder?)null);
        A.CallTo(() => folderService.GetRootFoldersAsync(A<Guid>.Ignored, A<CancellationToken>.Ignored)).Returns([]);

        var filterService = A.Fake<IApplicationFilterService>();
        A.CallTo(() => filterService.ShouldFilterAsync(A<string?>.Ignored, A<string?>.Ignored, A<CancellationToken>.Ignored)).Returns(false);

        var configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => configurationService.Configuration).Returns(new ClipMateConfiguration
        {
            Preferences = new PreferencesConfiguration
            {
                EnableAutoCaptureAtStartup = true,
                CaptureExistingClipboardAtStartup = false,
            },
        });

        var services = new ServiceCollection();
        services.AddSingleton(collectionService);
        services.AddSingleton(folderService);
        services.AddSingleton(filterService);
        services.AddSingleton(A.Fake<ISoundService>());
        services.AddSingleton(contextFactory);
        services.AddTransient<IClipAppendService>(p => new ClipAppendService(contextFactory, collectionService, A.Fake<IClipService>(),
            p.GetRequiredService<ISoundService>(), A.Fake<ILogger<ClipAppendService>>()));

        _serviceProvider = services.BuildServiceProvider();

        var clipboardService = A.Fake<IClipboardService>();
        _channel = Channel.CreateUnbounded<Clip>();
        A.CallTo(() => clipboardService.ClipsChannel).Returns(_channel.Reader);

        _coordinator = new ClipboardCoordinator(clipboardService, configurationService, contextFactory, _autoAppendService, _serviceProvider,
            new StrongReferenceMessenger(), A.Fake<ILogger<ClipboardCoordinator>>());

        await _coordinator.StartAsync(CancellationToken.None);
    }

    [After(Test)]
    public async Task StopCoordinatorAsync()
    {
        _channel.Writer.TryComplete();
        await _coordinator.StopAsync(CancellationToken.None);
        await _serviceProvider.DisposeAsync();
    }

    [Test]
    public async Task Capture_AfterGrowingClipSentToTrash_IsSavedAndBecomesTheGrowingClip()
    {
        _autoAppendService.Activate();
        var first = TextClip("https://example.com/first");
        await CaptureAsync(first, () => _autoAppendService.GrowingClipId == first.Id);
        await Assert.That(_autoAppendService.GrowingClipId).IsEqualTo(first.Id);

        await _clipRepository.DeleteAsync(first.Id);

        var second = TextClip("https://example.com/second");
        await CaptureAsync(second, () => _autoAppendService.GrowingClipId == second.Id);

        var saved = await _clipRepository.GetByIdAsync(second.Id);
        await Assert.That(saved).IsNotNull();
        await Assert.That(_autoAppendService.GrowingClipId).IsEqualTo(second.Id);
    }

    private async Task CaptureAsync(Clip clip, Func<bool> done)
    {
        await _channel.Writer.WriteAsync(clip);

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!done() && DateTime.UtcNow < deadline)
            await Task.Delay(20);
    }

    private static Clip TextClip(string text) => new()
    {
        Id = Guid.NewGuid(),
        Type = ClipType.Text,
        TextContent = text,
        ContentHash = text.GetHashCode().ToString("X8"),
        CapturedAt = DateTimeOffset.Now,
    };
}
