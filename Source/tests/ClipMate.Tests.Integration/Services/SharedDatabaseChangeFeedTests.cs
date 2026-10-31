using System.Collections.Concurrent;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data;
using ClipMate.Data.Services;
using FakeItEasy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Live change notifications on the serving side: every write to a Shared Database through the normal data path
/// (repositories and services on <see cref="DatabaseContextFactory" /> contexts) reports which collection changed.
/// </summary>
public class SharedDatabaseChangeFeedTests
{
    private static readonly TimeSpan _wait = TimeSpan.FromSeconds(10);
    private ClipMateConfiguration _configuration = null!;
    private IConfigurationService _configurationService = null!;
    private DatabaseContextFactory _contextFactory = null!;
    private string _directory = null!;
    private SharedDatabaseChangeFeed _feed = null!;
    private Guid _homeId;
    private Guid _workId;
    private Collection _workInbox = null!;
    private Collection _workArchive = null!;
    private Collection _homeInbox = null!;
    private readonly ConcurrentQueue<SharedCollectionChange> _changes = new();
    private int _shareListChanges;

    [Before(Test)]
    public async Task Setup()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"clipmate_changes_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        var workPath = Path.Combine(_directory, "work.db");
        var homePath = Path.Combine(_directory, "home.db");
        await CreateDatabaseAsync(workPath);
        await CreateDatabaseAsync(homePath);

        _configuration = new ClipMateConfiguration
        {
            Databases =
            {
                ["db_work"] = new DatabaseConfiguration { Name = "Work", FilePath = workPath, ShareWithPeers = true },
                ["db_home"] = new DatabaseConfiguration { Name = "Home", FilePath = homePath, ShareWithPeers = false },
            },
        };
        _configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => _configurationService.Configuration).Returns(_configuration);

        var monitor = new DatabaseChangeMonitor();
        _contextFactory = new DatabaseContextFactory(new NullServiceProvider(), _configurationService,
            NullLogger<DatabaseContextFactory>.Instance, monitor);

        var databaseIds = new DatabaseIdService(_configurationService);
        _workId = (await databaseIds.GetDatabaseIdAsync("db_work")).GetValueOrDefault();
        _homeId = (await databaseIds.GetDatabaseIdAsync("db_home")).GetValueOrDefault();

        var collections = _contextFactory.GetCollectionRepository("db_work");
        _workInbox = await collections.CreateAsync(new Collection { Id = Guid.NewGuid(), Title = "Inbox" });
        _workArchive = await collections.CreateAsync(new Collection { Id = Guid.NewGuid(), Title = "Archive" });
        _homeInbox = await _contextFactory.GetCollectionRepository("db_home").CreateAsync(new Collection { Id = Guid.NewGuid(), Title = "Inbox" });

        _feed = new SharedDatabaseChangeFeed(_configurationService, databaseIds, _contextFactory, monitor,
            NullLogger<SharedDatabaseChangeFeed>.Instance);
        _feed.CollectionChanged += OnCollectionChanged;
        _feed.SharedDatabasesChanged += OnSharedDatabasesChanged;
    }

    [After(Test)]
    public void Cleanup()
    {
        _feed.CollectionChanged -= OnCollectionChanged;
        _feed.SharedDatabasesChanged -= OnSharedDatabasesChanged;
        _feed.Dispose();
        _contextFactory.Dispose();
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_directory, true);
        }
        catch (IOException)
        {
            // Best effort
        }
    }

    [Test]
    public async Task CapturingAClipInASharedDatabase_ReportsItsCollection()
    {
        // Act
        await CaptureAsync("db_work", _workInbox.Id);

        // Assert
        await WaitForAsync(new SharedCollectionChange(_workId, _workInbox.Id));
    }

    [Test]
    public async Task CapturingAClipInADatabaseThatIsNotShared_ReportsNothing()
    {
        // Act
        await CaptureAsync("db_home", _homeInbox.Id);

        // Assert: a change in the Shared Database afterwards arrives alone
        await CaptureAsync("db_work", _workInbox.Id);
        await WaitForAsync(new SharedCollectionChange(_workId, _workInbox.Id));
        await Assert.That(_changes.Any(p => p.DatabaseId == _homeId || p.CollectionId == _homeInbox.Id)).IsFalse();
    }

    [Test]
    public async Task DeletingAClip_ReportsItsCollection()
    {
        // Arrange
        var clip = await CaptureAsync("db_work", _workInbox.Id);
        await WaitForAsync(new SharedCollectionChange(_workId, _workInbox.Id));
        _changes.Clear();

        // Act
        await _contextFactory.GetClipRepository("db_work").DeleteAsync(clip.Id);

        // Assert
        await WaitForAsync(new SharedCollectionChange(_workId, _workInbox.Id));
    }

    [Test]
    public async Task MovingAClip_ReportsBothCollections()
    {
        // Arrange
        var clip = await CaptureAsync("db_work", _workInbox.Id);
        await WaitForAsync(new SharedCollectionChange(_workId, _workInbox.Id));
        _changes.Clear();

        // Act
        await _contextFactory.GetClipRepository("db_work").MoveClipsToCollectionAsync([clip.Id], _workArchive.Id);

        // Assert
        await WaitForAsync(new SharedCollectionChange(_workId, _workArchive.Id));
        await WaitForAsync(new SharedCollectionChange(_workId, _workInbox.Id));
    }

    [Test]
    public async Task EditingAClip_ReportsItsCollection()
    {
        // Arrange
        var clip = await CaptureAsync("db_work", _workInbox.Id);
        await WaitForAsync(new SharedCollectionChange(_workId, _workInbox.Id));
        _changes.Clear();
        var repository = _contextFactory.GetClipRepository("db_work");
        var stored = await repository.GetByIdAsync(clip.Id);
        stored!.Title = "Renamed";

        // Act
        await repository.UpdateAsync(stored);

        // Assert
        await WaitForAsync(new SharedCollectionChange(_workId, _workInbox.Id));
    }

    [Test]
    public async Task EditingOnlyAClipsContent_ReportsItsCollection()
    {
        // Arrange
        var clip = await CaptureAsync("db_work", _workInbox.Id);
        await WaitForAsync(new SharedCollectionChange(_workId, _workInbox.Id));
        _changes.Clear();

        // Act: a new text blob for the clip, without touching the clip row
        var formats = await _contextFactory.GetClipDataRepository("db_work").GetByClipIdAsync(clip.Id);
        await _contextFactory.GetBlobRepository("db_work").CreateTextAsync(new BlobTxt
        {
            Id = Guid.NewGuid(), ClipId = clip.Id, ClipDataId = formats[0].Id, Data = "edited",
        });

        // Assert
        await WaitForAsync(new SharedCollectionChange(_workId, _workInbox.Id));
    }

    [Test]
    public async Task ChangingACollection_ReportsIt()
    {
        // Arrange
        var collections = _contextFactory.GetCollectionRepository("db_work");
        var archive = await collections.GetByIdAsync(_workArchive.Id);
        archive!.Title = "Old stuff";

        // Act
        await collections.UpdateAsync(archive);

        // Assert
        await WaitForAsync(new SharedCollectionChange(_workId, _workArchive.Id));
    }

    [Test]
    public async Task CapturingAClip_AlsoReportsTheSmartCollectionsOfThatDatabase()
    {
        // Arrange: a Smart collection lists clips by its own SQL, so any clip change may change it
        var today = await _contextFactory.GetCollectionRepository("db_work").CreateAsync(new Collection
        {
            Id = Guid.NewGuid(), Title = "Today", LmType = CollectionLmType.Virtual, Sql = "SELECT * FROM Clips",
        });
        await WaitForAsync(new SharedCollectionChange(_workId, today.Id));
        _changes.Clear();

        // Act
        await CaptureAsync("db_work", _workInbox.Id);

        // Assert
        await WaitForAsync(new SharedCollectionChange(_workId, today.Id));
    }

    [Test]
    public async Task SharingADatabase_RaisesSharedDatabasesChangedWhenTheConfigurationIsSaved()
    {
        // Arrange
        _configuration.Databases["db_home"].ShareWithPeers = true;

        // Act
        _configurationService.Saved += Raise.WithEmpty();

        // Assert
        await Assert.That(Volatile.Read(ref _shareListChanges)).IsEqualTo(1);
    }

    [Test]
    public async Task SavingTheConfigurationWithoutAShareChange_DoesNotRaiseSharedDatabasesChanged()
    {
        // Act
        _configuration.Preferences.EnableCachedDatabaseWrites = !_configuration.Preferences.EnableCachedDatabaseWrites;
        _configurationService.Saved += Raise.WithEmpty();

        // Assert
        await Assert.That(Volatile.Read(ref _shareListChanges)).IsEqualTo(0);
    }

    [Test]
    public async Task NoSubscriber_StopsWatching()
    {
        // Arrange
        _feed.CollectionChanged -= OnCollectionChanged;
        _feed.SharedDatabasesChanged -= OnSharedDatabasesChanged;

        // Act
        await CaptureAsync("db_work", _workInbox.Id);
        await Task.Delay(300);

        // Assert
        await Assert.That(_changes).IsEmpty();
        A.CallTo(_configurationService).Where(p => p.Method.Name == "remove_Saved").MustHaveHappened();
    }

    private void OnCollectionChanged(object? sender, SharedCollectionChangedEventArgs e) => _changes.Enqueue(e.Change);

    private void OnSharedDatabasesChanged(object? sender, EventArgs e) => Interlocked.Increment(ref _shareListChanges);

    private async Task WaitForAsync(SharedCollectionChange expected)
    {
        var deadline = DateTime.UtcNow + _wait;
        while (!_changes.Contains(expected) && DateTime.UtcNow < deadline)
            await Task.Delay(20);

        await Assert.That(_changes).Contains(expected);
    }

    private async Task<Clip> CaptureAsync(string databaseKey, Guid collectionId)
    {
        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            CollectionId = collectionId,
            Title = "Captured",
            Type = ClipType.Text,
            TextContent = "Hello",
            ContentHash = Guid.NewGuid().ToString("N"),
            CapturedAt = DateTimeOffset.Now,
        };

        return await _contextFactory.GetClipRepository(databaseKey).CreateAsync(clip);
    }

    private static async Task CreateDatabaseAsync(string path)
    {
        var options = new DbContextOptionsBuilder<ClipMateDbContext>().UseSqlite($"Data Source={path}").Options;
        await using var context = new ClipMateDbContext(options);
        await context.Database.EnsureCreatedAsync();
        await new DatabaseSchemaMigrationService().MigrateAsync(context);
    }

    /// <summary>
    /// Lets <see cref="DatabaseContextFactory" /> build repositories with their loggers.
    /// </summary>
    private sealed class NullServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType.IsGenericType && serviceType.GetGenericTypeDefinition() == typeof(ILogger<>)
                ? Activator.CreateInstance(typeof(NullLogger<>).MakeGenericType(serviceType.GetGenericArguments()))
                : null;
    }
}
