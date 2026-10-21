using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using ClipMate.Data;
using ClipMate.Data.Services;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClipMate.Tests.Unit.Services;

public class SharedDatabaseServiceTests
{
    private static readonly Guid _workId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _homeId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Dictionary<string, List<Collection>> _collections = [];
    private IClipContentService _clipContentService = null!;
    private IClipRepository _clipRepository = null!;
    private IClipService _clipService = null!;
    private ICollectionService _collectionService = null!;
    private ClipMateConfiguration _configuration = null!;
    private IDatabaseContextFactory _databaseContextFactory = null!;
    private IDatabaseIdService _databaseIdService = null!;
    private SharedDatabaseService _service = null!;

    [Before(Test)]
    public void Setup()
    {
        _configuration = new ClipMateConfiguration { Databases = [] };
        var configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => configurationService.Configuration).Returns(_configuration);
        _databaseIdService = A.Fake<IDatabaseIdService>();
        _collectionService = A.Fake<ICollectionService>();
        _clipService = A.Fake<IClipService>();
        _clipContentService = A.Fake<IClipContentService>();
        _clipRepository = A.Fake<IClipRepository>();
        _databaseContextFactory = A.Fake<IDatabaseContextFactory>();
        A.CallTo(() => _databaseContextFactory.GetClipRepository("db_work")).Returns(_clipRepository);
        _service = new SharedDatabaseService(configurationService, _databaseIdService, _collectionService, _clipService,
            _clipContentService, _databaseContextFactory, NullLogger<SharedDatabaseService>.Instance);
    }

    [Test]
    public async Task GetSharedDatabasesAsync_ListsOnlyDatabasesMarkedShareWithPeers()
    {
        // Arrange
        AddDatabase("db_work", "Work", _workId, true);
        AddDatabase("db_home", "Home", _homeId, false);

        // Act
        var shared = await _service.GetSharedDatabasesAsync();

        // Assert
        await Assert.That(shared).IsEquivalentTo([new SharedDatabase(_workId, "Work")]);
    }

    [Test]
    public async Task GetSharedDatabasesAsync_DatabaseWithoutDatabaseId_IsLeftOut()
    {
        // Arrange
        AddDatabase("db_work", "Work", null, true);

        // Act
        var shared = await _service.GetSharedDatabasesAsync();

        // Assert
        await Assert.That(shared).IsEmpty();
    }

    [Test]
    public async Task GetSharedDatabasesAsync_TwoSharedDatabasesWithTheSameDatabaseId_ServesOnlyTheFirst()
    {
        // Arrange: "Work copy" is a copied file that kept the Database ID.
        AddDatabase("db_work", "Work", _workId, true);
        AddDatabase("db_copy", "Work copy", _workId, true);

        // Act
        var shared = await _service.GetSharedDatabasesAsync();

        // Assert
        await Assert.That(shared).IsEquivalentTo([new SharedDatabase(_workId, "Work")]);
    }

    [Test]
    public async Task GetCollectionTreeAsync_SharedDatabase_ListsCollectionsFoldersAndSmartAndSqlCollectionsWithoutSql()
    {
        // Arrange
        AddDatabase("db_work", "Work", _workId, true);
        var inbox = new Collection { Id = Guid.NewGuid(), Title = "Inbox", LmType = CollectionLmType.Normal, SortKey = 10 };
        var safe = new Collection { Id = Guid.NewGuid(), Title = "Safe", LmType = CollectionLmType.Folder, SortKey = 20 };
        var projects = new Collection { Id = Guid.NewGuid(), ParentId = inbox.Id, Title = "Projects", LmType = CollectionLmType.Folder, SortKey = 5 };
        var today = new Collection { Id = Guid.NewGuid(), Title = "Today", LmType = CollectionLmType.Virtual, Sql = "SELECT * FROM Clips WHERE secret", SortKey = 30 };
        var bitmaps = new Collection
        {
            Id = Guid.NewGuid(), Title = "Bitmaps", LmType = CollectionLmType.Normal, ListType = CollectionListType.SqlBased,
            Sql = "SELECT * FROM Clips WHERE Type = 2", SortKey = 40,
        };
        A.CallTo(() => _collectionService.GetAllByDatabaseKeyAsync("db_work", A<CancellationToken>._))
            .Returns([inbox, safe, projects, today, bitmaps]);

        // Act
        var tree = await _service.GetCollectionTreeAsync(_workId);

        // Assert
        await Assert.That(tree).IsEquivalentTo([
            new SharedCollection(inbox.Id, null, "Inbox", SharedCollectionKind.Collection, 10),
            new SharedCollection(safe.Id, null, "Safe", SharedCollectionKind.Collection, 20),
            new SharedCollection(projects.Id, inbox.Id, "Projects", SharedCollectionKind.Folder, 5),
            new SharedCollection(today.Id, null, "Today", SharedCollectionKind.Smart, 30),
            new SharedCollection(bitmaps.Id, null, "Bitmaps", SharedCollectionKind.SqlBased, 40),
        ]);
    }

    [Test]
    public async Task GetCollectionTreeAsync_DatabaseNotShared_ReturnsNullWithoutReadingIt()
    {
        // Arrange
        AddDatabase("db_home", "Home", _homeId, false);

        // Act
        var tree = await _service.GetCollectionTreeAsync(_homeId);

        // Assert
        await Assert.That(tree).IsNull();
        A.CallTo(() => _collectionService.GetAllByDatabaseKeyAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task FindDatabaseIdConflictAsync_AnotherSharedDatabaseHasTheSameId_ReturnsIt()
    {
        // Arrange
        var work = new DatabaseConfiguration { Name = "Work", FilePath = @"C:\clips\work.db", ShareWithPeers = true };
        A.CallTo(() => _databaseIdService.GetDatabaseIdForFileAsync(@"C:\clips\work.db", A<CancellationToken>._)).Returns(_workId);
        A.CallTo(() => _databaseIdService.GetDatabaseIdForFileAsync(@"C:\clips\copy.db", A<CancellationToken>._)).Returns(_workId);

        // Act
        var conflict = await _service.FindDatabaseIdConflictAsync(@"C:\clips\copy.db", [work]);

        // Assert
        await Assert.That(conflict).IsSameReferenceAs(work);
    }

    [Test]
    public async Task FindDatabaseIdConflictAsync_DatabaseWithTheSameIdIsNotShared_ReturnsNull()
    {
        // Arrange
        var work = new DatabaseConfiguration { Name = "Work", FilePath = @"C:\clips\work.db", ShareWithPeers = false };
        A.CallTo(() => _databaseIdService.GetDatabaseIdForFileAsync(A<string>._, A<CancellationToken>._)).Returns(_workId);

        // Act
        var conflict = await _service.FindDatabaseIdConflictAsync(@"C:\clips\copy.db", [work]);

        // Assert
        await Assert.That(conflict).IsNull();
    }

    [Test]
    public async Task FindDatabaseIdConflictAsync_SharedDatabaseWithAnotherId_ReturnsNull()
    {
        // Arrange
        var home = new DatabaseConfiguration { Name = "Home", FilePath = @"C:\clips\home.db", ShareWithPeers = true };
        A.CallTo(() => _databaseIdService.GetDatabaseIdForFileAsync(@"C:\clips\home.db", A<CancellationToken>._)).Returns(_homeId);
        A.CallTo(() => _databaseIdService.GetDatabaseIdForFileAsync(@"C:\clips\work.db", A<CancellationToken>._)).Returns(_workId);

        // Act
        var conflict = await _service.FindDatabaseIdConflictAsync(@"C:\clips\work.db", [home]);

        // Assert
        await Assert.That(conflict).IsNull();
    }

    [Test]
    public async Task GetClipPageAsync_CollectionOfASharedDatabase_ReturnsThePageFromThatDatabase()
    {
        // Arrange
        AddDatabase("db_work", "Work", _workId, true);
        var inbox = AddCollection("db_work", new Collection { Id = Guid.NewGuid(), Title = "Inbox" });
        var page = new ClipPage([new Clip { Id = Guid.NewGuid(), Title = "Hello" }], 0, 40);
        A.CallTo(() => _clipRepository.GetPageByCollectionAsync(inbox.Id, 0, 25, A<CancellationToken>._)).Returns(page);

        // Act
        var result = await _service.GetClipPageAsync(_workId, inbox.Id, null, 0, 25);

        // Assert
        await Assert.That(result).IsSameReferenceAs(page);
    }

    [Test]
    public async Task GetClipPageAsync_FolderOfASharedDatabase_ReturnsTheFolderPage()
    {
        // Arrange
        AddDatabase("db_work", "Work", _workId, true);
        var inbox = AddCollection("db_work", new Collection { Id = Guid.NewGuid(), Title = "Inbox" });
        var projects = AddCollection("db_work", new Collection { Id = Guid.NewGuid(), ParentId = inbox.Id, Title = "Projects", LmType = CollectionLmType.Folder });
        var page = new ClipPage([], 0, 0);
        A.CallTo(() => _clipRepository.GetPageByFolderAsync(projects.Id, 0, 25, A<CancellationToken>._)).Returns(page);

        // Act
        var result = await _service.GetClipPageAsync(_workId, inbox.Id, projects.Id, 0, 25);

        // Assert
        await Assert.That(result).IsSameReferenceAs(page);
    }

    [Test]
    public async Task GetClipPageAsync_SqlBasedCollection_RunsThePeersOwnStoredSqlAndPagesTheResult()
    {
        // Arrange
        AddDatabase("db_work", "Work", _workId, true);
        var bitmaps = AddCollection("db_work", new Collection
        {
            Id = Guid.NewGuid(), Title = "Bitmaps", ListType = CollectionListType.SqlBased, Sql = "SELECT * FROM Clips WHERE Type = 2", RetentionLimit = 200,
        });
        var clips = Enumerable.Range(1, 5).Select(p => new Clip { Id = Guid.NewGuid(), Title = $"Clip {p}" }).ToList();
        A.CallTo(() => _clipService.ExecuteSqlQueryAsync("db_work", "SELECT * FROM Clips WHERE Type = 2", 200, A<CancellationToken>._)).Returns(clips);

        // Act
        var result = await _service.GetClipPageAsync(_workId, bitmaps.Id, null, 2, 2);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.TotalCount).IsEqualTo(5);
        await Assert.That(result.Offset).IsEqualTo(2);
        await Assert.That(result.Clips.Select(p => p.Id)).IsEquivalentTo([clips[2].Id, clips[3].Id]);
    }

    [Test]
    public async Task GetClipPageAsync_DatabaseNotShared_ReturnsNullWithoutReadingClips()
    {
        // Arrange
        AddDatabase("db_home", "Home", _homeId, false);

        // Act
        var result = await _service.GetClipPageAsync(_homeId, Guid.NewGuid(), null, 0, 25);

        // Assert
        await Assert.That(result).IsNull();
        A.CallTo(() => _databaseContextFactory.GetClipRepository(A<string>._)).MustNotHaveHappened();
        A.CallTo(() => _clipService.ExecuteSqlQueryAsync(A<string>._, A<string>._, A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task GetClipPageAsync_CollectionOfAnotherDatabase_ReturnsNull()
    {
        // Arrange: the collection exists, but in an unshared Database.
        AddDatabase("db_work", "Work", _workId, true);
        AddDatabase("db_home", "Home", _homeId, false);
        var diary = AddCollection("db_home", new Collection { Id = Guid.NewGuid(), Title = "Diary" });

        // Act
        var result = await _service.GetClipPageAsync(_workId, diary.Id, null, 0, 25);

        // Assert
        await Assert.That(result).IsNull();
        A.CallTo(() => _databaseContextFactory.GetClipRepository(A<string>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task GetClipContentAsync_ClipOfASharedDatabase_ReturnsItsContentAsStored()
    {
        // Arrange
        AddDatabase("db_work", "Work", _workId, true);
        var clip = new Clip { Id = Guid.NewGuid(), Encrypted = true };
        var content = new ClipContent(clip, [], [new BlobTxt { ClipId = clip.Id, Data = "Y2lwaGVydGV4dA==" }], [], [], []);
        A.CallTo(() => _clipContentService.GetClipContentAsync("db_work", clip.Id, A<CancellationToken>._)).Returns(content);

        // Act
        var result = await _service.GetClipContentAsync(_workId, clip.Id);

        // Assert
        await Assert.That(result).IsSameReferenceAs(content);
    }

    [Test]
    public async Task GetClipContentAsync_DeletedClip_ReturnsNull()
    {
        // Arrange
        AddDatabase("db_work", "Work", _workId, true);
        var clip = new Clip { Id = Guid.NewGuid(), Del = true };
        A.CallTo(() => _clipContentService.GetClipContentAsync("db_work", clip.Id, A<CancellationToken>._))
            .Returns(new ClipContent(clip, [], [], [], [], []));

        // Act
        var result = await _service.GetClipContentAsync(_workId, clip.Id);

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetClipContentAsync_DatabaseNotShared_ReturnsNullWithoutReadingIt()
    {
        // Arrange
        AddDatabase("db_home", "Home", _homeId, false);

        // Act
        var result = await _service.GetClipContentAsync(_homeId, Guid.NewGuid());

        // Assert
        await Assert.That(result).IsNull();
        A.CallTo(_clipContentService).MustNotHaveHappened();
    }

    private Collection AddCollection(string key, Collection collection)
    {
        if (!_collections.TryGetValue(key, out var list))
        {
            list = [];
            _collections[key] = list;
            A.CallTo(() => _collectionService.GetAllByDatabaseKeyAsync(key, A<CancellationToken>._)).Returns(list);
        }

        list.Add(collection);
        return collection;
    }

    private void AddDatabase(string key, string name, Guid? databaseId, bool shared)
    {
        _configuration.Databases[key] = new DatabaseConfiguration { Name = name, FilePath = $@"C:\clips\{key}.db", ShareWithPeers = shared };
        A.CallTo(() => _databaseIdService.GetDatabaseIdAsync(key, A<CancellationToken>._)).Returns(databaseId);
        A.CallTo(() => _databaseIdService.GetDatabaseIdForFileAsync($@"C:\clips\{key}.db", A<CancellationToken>._)).Returns(databaseId);
    }
}
