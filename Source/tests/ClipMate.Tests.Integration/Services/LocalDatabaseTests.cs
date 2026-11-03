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
    public async Task GetClipPageAsync_CollectionIncludingFolders_PagesTheCollectionAndItsFoldersAtAnyDepthNewestFirst()
    {
        // Arrange
        var deep = await AddCollectionAsync(new Collection { Title = "Deep", SortKey = 1, LmType = CollectionLmType.Folder, ParentId = _work.Id });
        var other = await AddCollectionAsync(new Collection { Title = "Other", SortKey = 2 });
        await AddClipAsync("In the collection", _inbox.Id, sortKey: 5);
        await AddClipAsync("Filed in Work", _inbox.Id, _work.Id, sortKey: 4);
        await AddClipAsync("Filed in Deep", deep.Id, deep.Id, sortKey: 3);
        await AddClipAsync("Deleted from Work", _inbox.Id, _work.Id, sortKey: 6, deleted: true);
        await AddClipAsync("Elsewhere", other.Id, sortKey: 7);
        await AddClipAsync("Oldest", _inbox.Id, sortKey: 1);

        // Act
        var pages = await LoadAllAsync(new ClipListScope.Collection(_inbox.Id, IncludeFolders: true), 2);

        // Assert
        await Assert.That(string.Join(" | ", pages.Select(Titles))).IsEqualTo("In the collection, Filed in Work | Filed in Deep, Oldest");
        await Assert.That(pages[0].TotalCount).IsEqualTo(4);
    }

    [Test]
    public async Task GetClipPageAsync_Trash_PagesNewestDeletedFirstThenUndatedBySortKey()
    {
        // Arrange
        await AddClipAsync("Kept", _inbox.Id, sortKey: 10);
        await AddClipAsync("Deleted 3 days ago", _inbox.Id, sortKey: 9, deletedAt: DateTimeOffset.UtcNow.AddDays(-3));
        await AddClipAsync("Undated low", _inbox.Id, sortKey: 2, deleted: true);
        await AddClipAsync("Deleted yesterday", _work.Id, sortKey: 1, deletedAt: DateTimeOffset.UtcNow.AddDays(-1));
        await AddClipAsync("Undated high", _inbox.Id, sortKey: 8, deleted: true);

        // Act
        var trash = await LoadAllAsync(new ClipListScope.Trash(), 2);
        var inbox = await _database.GetClipPageAsync(new ClipListScope.Collection(_inbox.Id), 0, 10);

        // Assert
        await Assert.That(string.Join(" | ", trash.Select(Titles))).IsEqualTo("Deleted yesterday, Deleted 3 days ago | Undated high, Undated low");
        await Assert.That(trash[0].TotalCount).IsEqualTo(4);
        await Assert.That(Titles(inbox)).IsEqualTo("Kept");
    }

    [Test]
    public async Task GetClipPageAsync_LaterPages_CountNoFurtherThanThemselves()
    {
        // Arrange
        var sqlCollection = await AddCollectionAsync(new Collection
        {
            Title = "Not deleted", SortKey = 500, ListType = CollectionListType.SqlBased, RetentionLimit = 200,
            Sql = "SELECT * FROM Clips WHERE Del = 0",
        });

        for (var i = 1; i <= 7; i++)
        {
            await AddClipAsync($"Filed {i}", _inbox.Id, _work.Id, sortKey: i);
            await AddClipAsync($"Deleted {i}", _inbox.Id, sortKey: i, deletedAt: DateTimeOffset.UtcNow.AddMinutes(-i));
        }

        ClipListScope[] scopes =
        [
            new ClipListScope.Collection(_inbox.Id),
            new ClipListScope.Folder(_inbox.Id, _work.Id),
            new ClipListScope.Collection(_inbox.Id, IncludeFolders: true),
            new ClipListScope.Trash(),
            new ClipListScope.Collection(sqlCollection.Id),
        ];

        foreach (var item in scopes)
        {
            // Act
            var pages = await LoadAllAsync(item, 2);

            // Assert: only the first page counts the whole list; a later page counts up to itself, plus one when more follow
            await Assert.That($"{item}: {string.Join(",", pages.Select(p => p.TotalCount))}").IsEqualTo($"{item}: 7,5,7,7");
            await Assert.That(pages.Sum(p => p.Clips.Count)).IsEqualTo(7);
            await Assert.That(pages[^1].HasMore).IsFalse();
        }
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
        new(_databaseKey, new DatabaseConfiguration { Name = "My Clips", FilePath = filePath }, _factory, _searchService);


    private async Task<Collection> AddCollectionAsync(Collection collection)
    {
        collection.Id = Guid.NewGuid();
        collection.CreatedAt = DateTime.UtcNow;
        DbContext.Collections.Add(collection);
        await DbContext.SaveChangesAsync();
        return collection;
    }

    /// <summary>
    /// Loads every page of <paramref name="scope" />, <paramref name="pageSize" /> clips at a time, as the clip list does.
    /// </summary>
    private async Task<List<ClipPage>> LoadAllAsync(ClipListScope scope, int pageSize)
    {
        var pages = new List<ClipPage>();
        var offset = 0;
        while (true)
        {
            var page = await _database.GetClipPageAsync(scope, offset, pageSize);
            pages.Add(page);
            offset += page.Clips.Count;
            if (!page.HasMore)
                return pages;
        }
    }

    private async Task<Clip> AddClipAsync(string title, Guid collectionId, Guid? folderId = null, int sortKey = 0, DateTimeOffset? deletedAt = null,
        bool deleted = false)
    {
        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            Title = title,
            CollectionId = collectionId,
            FolderId = folderId,
            SortKey = sortKey,
            CapturedAt = DateTimeOffset.UtcNow.AddDays(-10),
            Del = deleted || deletedAt != null,
            DelDate = deletedAt,
        };

        DbContext.Clips.Add(clip);
        await DbContext.SaveChangesAsync();
        return clip;
    }

    private static string Titles(ClipPage page) => string.Join(", ", page.Clips.Select(p => p.Title));
}
