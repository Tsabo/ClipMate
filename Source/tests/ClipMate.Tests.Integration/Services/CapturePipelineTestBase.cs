using System.Collections.Concurrent;
using System.Threading.Channels;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services.Abstractions;
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
/// The capture pipeline over a real database: captures go in through the clipboard service's channel (or a Manual
/// Capture), and the clips saved in the active collection, <see cref="Inbox" />, come out.
/// </summary>
public abstract class CapturePipelineTestBase : IntegrationTestBase
{
    private const string _databaseKey = "db_capture";

    private readonly ConcurrentBag<Guid> _addedClipIds = [];
    private Channel<Clip> _channel = null!;
    private IClipboardService _clipboardService = null!;
    private ClipboardCoordinator _coordinator = null!;
    private int _ignoreSounds;
    private StrongReferenceMessenger _messenger = null!;
    private ServiceProvider _serviceProvider = null!;

    protected AutoAppendService AutoAppendService { get; } = new();

    protected ClipRepository ClipRepository { get; private set; } = null!;

    /// <summary>
    /// The active collection captures go into. It rejects duplicates, as Inbox does by default.
    /// </summary>
    protected Collection Inbox { get; private set; } = null!;

    /// <summary>
    /// The collection captures go into; <see cref="Inbox" /> unless a test switches it.
    /// </summary>
    protected Collection ActiveCollection { get; set; } = null!;

    [Before(Test)]
    public async Task SetupCoordinatorAsync()
    {
        Inbox = new Collection
        {
            Id = Guid.NewGuid(),
            Name = "Inbox",
            CreatedAt = DateTime.UtcNow,
            AcceptNewClips = true,
            AcceptDuplicates = false,
        };

        DbContext.Collections.Add(Inbox);
        await DbContext.SaveChangesAsync();
        ActiveCollection = Inbox;

        ClipRepository = new ClipRepository(DbContext, A.Fake<ILogger<ClipRepository>>());

        var contextFactory = A.Fake<IDatabaseContextFactory>();
        A.CallTo(() => contextFactory.GetClipRepository(_databaseKey)).Returns(ClipRepository);
        A.CallTo(() => contextFactory.GetClipDataRepository(_databaseKey)).Returns(new ClipDataRepository(DbContext));
        A.CallTo(() => contextFactory.GetBlobRepository(_databaseKey)).Returns(new BlobRepository(DbContext));

        var collectionService = A.Fake<ICollectionService>();
        A.CallTo(() => collectionService.GetActiveDatabaseKey()).Returns(_databaseKey);
        A.CallTo(() => collectionService.GetActiveAsync(A<CancellationToken>.Ignored)).ReturnsLazily(() => ActiveCollection);

        var folderService = A.Fake<IFolderService>();
        A.CallTo(() => folderService.GetActiveAsync(A<CancellationToken>.Ignored)).Returns((Folder?)null);
        A.CallTo(() => folderService.GetRootFoldersAsync(A<Guid>.Ignored, A<CancellationToken>.Ignored)).Returns([]);

        var filterService = A.Fake<IApplicationFilterService>();
        A.CallTo(() => filterService.ShouldFilterAsync(A<string?>.Ignored, A<string?>.Ignored, A<CancellationToken>.Ignored)).Returns(false);

        var soundService = A.Fake<ISoundService>();
        A.CallTo(() => soundService.PlaySoundAsync(SoundEvent.Ignore, A<CancellationToken>.Ignored))
            .Invokes(() => Interlocked.Increment(ref _ignoreSounds));

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
        services.AddSingleton(soundService);
        services.AddSingleton(contextFactory);
        services.AddTransient<IClipAppendService>(_ => new ClipAppendService(contextFactory, collectionService, A.Fake<IClipService>(),
            soundService, A.Fake<ILogger<ClipAppendService>>()));

        _serviceProvider = services.BuildServiceProvider();

        _clipboardService = A.Fake<IClipboardService>();
        _channel = Channel.CreateUnbounded<Clip>();
        A.CallTo(() => _clipboardService.ClipsChannel).Returns(_channel.Reader);

        _messenger = new StrongReferenceMessenger();
        _messenger.Register<ClipAddedEvent>(this, (_, message) => _addedClipIds.Add(message.Clip.Id));

        _coordinator = new ClipboardCoordinator(_clipboardService, configurationService, contextFactory, AutoAppendService, _serviceProvider,
            _messenger, A.Fake<ILogger<ClipboardCoordinator>>());

        await _coordinator.StartAsync(CancellationToken.None);
    }

    [After(Test)]
    public async Task StopCoordinatorAsync()
    {
        _channel.Writer.TryComplete();
        await _coordinator.StopAsync(CancellationToken.None);
        await _serviceProvider.DisposeAsync();
    }

    /// <summary>
    /// Captures <paramref name="clip" /> as a copy made elsewhere, and waits until it is saved or rejected.
    /// </summary>
    protected Task CaptureAsync(Clip clip) => CaptureAsync(clip, null);

    /// <summary>
    /// Captures <paramref name="clip" /> and waits until <paramref name="done" /> is true, or, when null, until the clip
    /// is saved or rejected.
    /// </summary>
    protected async Task CaptureAsync(Clip clip, Func<bool>? done)
    {
        var ignoredBefore = Volatile.Read(ref _ignoreSounds);
        await _channel.Writer.WriteAsync(clip);
        await WaitAsync(done ?? (() => _addedClipIds.Contains(clip.Id) || Volatile.Read(ref _ignoreSounds) > ignoredBefore));
    }

    /// <summary>
    /// Captures <paramref name="clip" /> with the Manual Capture hotkey, and waits until it is saved or rejected.
    /// </summary>
    protected async Task ManualCaptureAsync(Clip clip)
    {
        var ignoredBefore = Volatile.Read(ref _ignoreSounds);
        A.CallTo(() => _clipboardService.GetCurrentClipboardContentAsync(ManualCaptureKind.CaptureHotkey, A<CancellationToken>.Ignored)).Returns(clip);
        _messenger.Send(new ManualCaptureClipboardEvent());
        await WaitAsync(() => _addedClipIds.Contains(clip.Id) || Volatile.Read(ref _ignoreSounds) > ignoredBefore);
    }

    /// <summary>
    /// Adds a collection to the database.
    /// </summary>
    protected async Task<Collection> AddCollectionAsync(string name, bool acceptDuplicates)
    {
        var collection = new Collection
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatedAt = DateTime.UtcNow,
            AcceptNewClips = true,
            AcceptDuplicates = acceptDuplicates,
        };

        DbContext.Collections.Add(collection);
        await DbContext.SaveChangesAsync();
        return collection;
    }

    /// <summary>
    /// True when <paramref name="clip" /> was saved as a clip of its own, outside the Trash.
    /// </summary>
    protected async Task<bool> IsSavedAsync(Clip clip) => await ClipRepository.GetByIdAsync(clip.Id) != null;

    protected static Clip TextClip(string text) => new()
    {
        Id = Guid.NewGuid(),
        Type = ClipType.Text,
        TextContent = text,
        ContentHash = text.GetHashCode().ToString("X8"),
        CapturedAt = DateTimeOffset.Now,
    };

    private static async Task WaitAsync(Func<bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!done() && DateTime.UtcNow < deadline)
            await Task.Delay(20);
    }
}
