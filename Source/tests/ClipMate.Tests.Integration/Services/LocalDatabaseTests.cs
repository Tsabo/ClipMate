using ClipMate.Core.Constants;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Search;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data;
using ClipMate.Data.Repositories;
using ClipMate.Data.Services;
using ClipMate.Platform.Services;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// <see cref="LocalDatabase" /> against a real SQLite Database.
/// </summary>
public class LocalDatabaseTests : IntegrationTestBase
{
    private const string _databaseKey = "db_local001";

    private IDatabaseContextFactory _factory = null!;
    private string _databaseFile = null!;
    private ISearchService _searchService = null!;
    private LocalDatabase _database = null!;
    private Collection _inbox = null!;
    private Collection _work = null!;

    [Before(Test)]
    public async Task SetupDatabaseAsync()
    {
        _factory = A.Fake<IDatabaseContextFactory>();
        A.CallTo(() => _factory.GetClipRepository(_databaseKey)).Returns(new ClipRepository(DbContext, A.Fake<ILogger<ClipRepository>>()));
        A.CallTo(() => _factory.GetClipDataRepository(_databaseKey)).Returns(new ClipDataRepository(DbContext));
        A.CallTo(() => _factory.GetBlobRepository(_databaseKey)).Returns(new BlobRepository(DbContext));
        A.CallTo(() => _factory.GetCollectionRepository(_databaseKey)).Returns(new CollectionRepository(DbContext));

        // The context is in memory; the configured file only has to exist for the Database to be available
        _databaseFile = Path.GetTempFileName();
        _searchService = A.Fake<ISearchService>();
        _database = CreateDatabase(_databaseFile);

        _inbox = await AddCollectionAsync(new Collection { Title = "Inbox", SortKey = 100 });
        _work = await AddCollectionAsync(new Collection { Title = "Work", SortKey = 1, LmType = CollectionLmType.Folder, ParentId = _inbox.Id });
    }

    [After(Test)]
    public void CleanupDatabaseFile() => File.Delete(_databaseFile);

    [Test]
    public async Task GetTreeAsync_ListsCollectionsAndFoldersFlatWithTheirKinds()
    {
        // Arrange
        var safe = await AddCollectionAsync(new Collection { Title = "Safe", SortKey = 200, LmType = CollectionLmType.Folder });
        var today = await AddCollectionAsync(new Collection { Title = "Today", SortKey = 300, ListType = CollectionListType.Smart });

        // Act
        var tree = await _database.GetTreeAsync();

        // Assert
        await Assert.That(tree.Single(p => p.Id == _inbox.Id)).IsEqualTo(new CollectionEntry(_inbox.Id, null, "Inbox", CollectionEntryKind.Collection, 100));
        await Assert.That(tree.Single(p => p.Id == _work.Id)).IsEqualTo(new CollectionEntry(_work.Id, _inbox.Id, "Work", CollectionEntryKind.Folder, 1));
        await Assert.That(tree.Single(p => p.Id == safe.Id).Kind).IsEqualTo(CollectionEntryKind.Collection);
        await Assert.That(tree.Single(p => p.Id == today.Id).Kind).IsEqualTo(CollectionEntryKind.Smart);
    }

    [Test]
    public async Task GetClipPageAsync_Collection_PagesItsClipsNewestFirst()
    {
        // Arrange
        await AddClipAsync("Oldest", _inbox.Id, sortKey: 1);
        await AddClipAsync("Middle", _inbox.Id, sortKey: 2);
        await AddClipAsync("Newest", _inbox.Id, sortKey: 3);

        // Act
        var first = await _database.GetClipPageAsync(new ClipListScope.Collection(_inbox.Id), 0, 2);
        var second = await _database.GetClipPageAsync(new ClipListScope.Collection(_inbox.Id), 2, 2);

        // Assert
        await Assert.That(Titles(first)).IsEqualTo("Newest, Middle");
        await Assert.That(first.TotalCount).IsEqualTo(3);
        await Assert.That(first.HasMore).IsTrue();
        await Assert.That(Titles(second)).IsEqualTo("Oldest");
    }

    [Test]
    public async Task GetClipPageAsync_Folder_ListsOnlyThatFolder()
    {
        // Arrange
        await AddClipAsync("In the collection", _inbox.Id, sortKey: 1);
        await AddClipAsync("In Work", _inbox.Id, _work.Id, sortKey: 2);

        // Act
        var page = await _database.GetClipPageAsync(new ClipListScope.Folder(_inbox.Id, _work.Id), 0, 10);

        // Assert
        await Assert.That(Titles(page)).IsEqualTo("In Work");
    }

    [Test]
    public async Task GetClipPageAsync_CollectionIncludingFolders_ListsTheCollectionAndItsFolders()
    {
        // Arrange
        await AddClipAsync("In the collection", _inbox.Id, sortKey: 1);
        await AddClipAsync("In Work", _inbox.Id, _work.Id, sortKey: 2);

        // Act
        var page = await _database.GetClipPageAsync(new ClipListScope.Collection(_inbox.Id, IncludeFolders: true), 0, 10);

        // Assert
        await Assert.That(string.Join(", ", page.Clips.Select(p => p.Title).Order())).IsEqualTo("In the collection, In Work");
        await Assert.That(page.TotalCount).IsEqualTo(2);
    }

    [Test]
    public async Task GetClipPageAsync_Trash_ListsDeletedClipsMostRecentlyDeletedFirst()
    {
        // Arrange
        await AddClipAsync("Kept", _inbox.Id, sortKey: 1);
        await AddClipAsync("Deleted earlier", _inbox.Id, sortKey: 2, deletedAt: DateTimeOffset.UtcNow.AddDays(-2));
        await AddClipAsync("Deleted later", _inbox.Id, sortKey: 3, deletedAt: DateTimeOffset.UtcNow.AddDays(-1));

        // Act
        var trash = await _database.GetClipPageAsync(new ClipListScope.Trash(), 0, 10);
        var inbox = await _database.GetClipPageAsync(new ClipListScope.Collection(_inbox.Id), 0, 10);

        // Assert
        await Assert.That(Titles(trash)).IsEqualTo("Deleted later, Deleted earlier");
        await Assert.That(Titles(inbox)).IsEqualTo("Kept");
    }

    [Test]
    public async Task GetClipPageAsync_SqlCollection_RunsItsStoredSql()
    {
        // Arrange
        var sqlCollection = await AddCollectionAsync(new Collection
        {
            Title = "Starts with A", SortKey = 500, ListType = CollectionListType.SqlBased, RetentionLimit = 200,
            Sql = "SELECT * FROM Clips WHERE Title LIKE 'A%'",
        });

        await AddClipAsync("Apple", _inbox.Id, sortKey: 1);
        await AddClipAsync("Banana", _inbox.Id, sortKey: 2);

        // Act
        var page = await _database.GetClipPageAsync(new ClipListScope.Collection(sqlCollection.Id), 0, 10);

        // Assert
        await Assert.That(Titles(page)).IsEqualTo("Apple");
    }

    [Test]
    public async Task GetClipContentAsync_ReturnsTheClipWithItsFormatsOrNullForAnUnknownClip()
    {
        // Arrange
        var clip = await AddClipAsync("With text", _inbox.Id, sortKey: 1);
        var format = new ClipData { Id = Guid.NewGuid(), ClipId = clip.Id, FormatName = "CF_UNICODETEXT", Format = 13, StorageType = StorageType.Text };
        DbContext.ClipData.Add(format);
        DbContext.BlobTxt.Add(new BlobTxt { Id = Guid.NewGuid(), ClipId = clip.Id, ClipDataId = format.Id, Data = "Hello" });
        await DbContext.SaveChangesAsync();

        // Act
        var content = await _database.GetClipContentAsync(clip.Id);
        var unknown = await _database.GetClipContentAsync(Guid.NewGuid());

        // Assert
        await Assert.That(content!.Clip.Id).IsEqualTo(clip.Id);
        await Assert.That(content.Formats.Single().Id).IsEqualTo(format.Id);
        await Assert.That(content.TextBlobs.Single().Data).IsEqualTo("Hello");
        await Assert.That(unknown).IsNull();
    }

    [Test]
    public async Task SearchAsync_SearchesThisDatabase()
    {
        // Arrange
        var filters = new SearchFilters { TitleQuery = "budget" };
        var matches = new SearchMatches([], 0);
        A.CallTo(() => _searchService.SearchDatabaseAsync(_databaseKey, filters, 1000, A<CancellationToken>._)).Returns(matches);

        // Act
        var result = await _database.SearchAsync(filters, 1000);

        // Assert
        await Assert.That(result).IsSameReferenceAs(matches);
    }

    [Test]
    public async Task Reads_DatabaseFileMissing_AreUnavailableAndCreateNoFile()
    {
        // Arrange
        var missingFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        var database = CreateDatabase(missingFile);

        // Act & Assert
        await Assert.That(database.IsAvailable).IsFalse();
        await Assert.That(async () => await database.GetTreeAsync()).Throws<DatabaseUnavailableException>();
        await Assert.That(async () => await database.GetClipPageAsync(new ClipListScope.Collection(_inbox.Id), 0, 10)).Throws<DatabaseUnavailableException>();
        await Assert.That(async () => await database.GetClipContentAsync(Guid.NewGuid())).Throws<DatabaseUnavailableException>();
        await Assert.That(async () => await database.SearchAsync(new SearchFilters())).Throws<DatabaseUnavailableException>();
        await Assert.That(File.Exists(missingFile)).IsFalse();
    }

    [Test]
    public async Task Properties_LocalDatabase_IsWritableAndNotRemote()
    {
        // Assert
        await Assert.That(_database.Key).IsEqualTo(_databaseKey);
        await Assert.That(_database.Name).IsEqualTo("My Clips");
        await Assert.That(_database.IsAvailable).IsTrue();
        await Assert.That(_database.IsReadOnly).IsFalse();
        await Assert.That(_database.Remote).IsNull();
    }

    private LocalDatabase CreateDatabase(string filePath) =>
        new(_databaseKey, new DatabaseConfiguration { Name = "My Clips", FilePath = filePath }, _factory, CreateClipService(), _searchService);

    private ClipService CreateClipService()
    {
#pragma warning disable CA2000 // Test objects don't require explicit disposal
        var blobCacheService = new DecryptedBlobCacheService(new MemoryCache(new MemoryCacheOptions()), A.Fake<ILogger<DecryptedBlobCacheService>>());
#pragma warning restore CA2000

        return new ClipService(_factory, A.Fake<IConfigurationService>(), A.Fake<IClipboardService>(), A.Fake<ITemplateService>(),
            new AesEncryptionService(), blobCacheService, A.Fake<IClipContentService>(), A.Fake<IMessenger>(), A.Fake<ILogger<ClipService>>());
    }

    private async Task<Collection> AddCollectionAsync(Collection collection)
    {
        collection.Id = Guid.NewGuid();
        collection.CreatedAt = DateTime.UtcNow;
        DbContext.Collections.Add(collection);
        await DbContext.SaveChangesAsync();
        return collection;
    }

    private async Task<Clip> AddClipAsync(string title, Guid collectionId, Guid? folderId = null, int sortKey = 0, DateTimeOffset? deletedAt = null)
    {
        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            Title = title,
            CollectionId = collectionId,
            FolderId = folderId,
            SortKey = sortKey,
            CapturedAt = DateTimeOffset.UtcNow.AddDays(-10),
            Del = deletedAt != null,
            DelDate = deletedAt,
        };

        DbContext.Clips.Add(clip);
        await DbContext.SaveChangesAsync();
        return clip;
    }

    private static string Titles(ClipPage page) => string.Join(", ", page.Clips.Select(p => p.Title));
}
