using ClipMate.Core.Models;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data;
using ClipMate.Data.Services;
using FakeItEasy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// <see cref="CollectionService" /> queries against a real SQLite database, where a filter EF can't translate fails.
/// </summary>
public class CollectionServiceDatabaseTests
{
    private const string _databaseKey = "db_work";
    private SqliteConnection _connection = null!;
    private DbContextOptions<ClipMateDbContext> _options = null!;

    [Before(Test)]
    public async Task SetupAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<ClipMateDbContext>().UseSqlite(_connection).Options;

        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
    }

    [After(Test)]
    public async Task CleanupAsync() => await _connection.DisposeAsync();

    private ClipMateDbContext CreateContext() => new(_options);

    private CollectionService CreateCollectionService()
    {
        var databaseManager = A.Fake<IDatabaseManager>();
        A.CallTo(() => databaseManager.CreateDatabaseContext(_databaseKey)).ReturnsLazily(CreateContext);
        return new CollectionService(databaseManager, A.Fake<IDatabaseContextFactory>());
    }

    private async Task<Collection> AddAsync(string title, int sortKey, Guid? parentId = null,
        CollectionLmType lmType = CollectionLmType.Normal, CollectionListType listType = CollectionListType.Normal, bool favorite = false)
    {
        var collection = new Collection
        {
            Id = Guid.NewGuid(),
            Title = title,
            SortKey = sortKey,
            ParentId = parentId,
            LmType = lmType,
            ListType = listType,
            Favorite = favorite,
            CreatedAt = DateTime.UtcNow,
        };

        await using var context = CreateContext();
        context.Collections.Add(collection);
        await context.SaveChangesAsync();
        return collection;
    }

    private async Task<string> TopLevelOrderAsync()
    {
        await using var context = CreateContext();
        var collections = await context.Collections.ToListAsync();
        return string.Join(", ", collections
            .Where(p => !p.IsVirtual && p.ParentId == null)
            .OrderBy(p => p.SortKey)
            .ThenBy(p => p.Title, StringComparer.Ordinal)
            .Select(p => p.Title));
    }

    private async Task<int> SortKeyOfAsync(Guid id)
    {
        await using var context = CreateContext();
        return (await context.Collections.SingleAsync(p => p.Id == id)).SortKey;
    }

    [Test]
    public async Task MoveCollectionDownAsync_MiddleCollection_SwapsWithTheNextOne()
    {
        // Arrange
        var inbox = await AddAsync("InBox", 100);
        await AddAsync("Safe", 200);
        await AddAsync("Overflow", 300);

        // Act
        var moved = await CreateCollectionService().MoveCollectionDownAsync(inbox.Id, _databaseKey);

        // Assert
        await Assert.That(moved).IsTrue();
        await Assert.That(await TopLevelOrderAsync()).IsEqualTo("Safe, InBox, Overflow");
    }

    [Test]
    public async Task MoveCollectionUpAsync_MiddleCollection_SwapsWithThePreviousOne()
    {
        // Arrange
        await AddAsync("InBox", 100);
        var safe = await AddAsync("Safe", 200);
        await AddAsync("Overflow", 300);

        // Act
        var moved = await CreateCollectionService().MoveCollectionUpAsync(safe.Id, _databaseKey);

        // Assert
        await Assert.That(moved).IsTrue();
        await Assert.That(await TopLevelOrderAsync()).IsEqualTo("Safe, InBox, Overflow");
    }

    [Test]
    public async Task MoveCollectionDownAsync_FolderAndVirtualCollectionInBetween_SwapsWithTheNextTopLevelCollection()
    {
        // Arrange
        var inbox = await AddAsync("InBox", 100);
        var folder = await AddAsync("Letters", 150, inbox.Id, CollectionLmType.Folder);
        var today = await AddAsync("Today", 160, listType: CollectionListType.Smart);
        await AddAsync("Safe", 200);

        // Act
        var moved = await CreateCollectionService().MoveCollectionDownAsync(inbox.Id, _databaseKey);

        // Assert
        await Assert.That(moved).IsTrue();
        await Assert.That(await TopLevelOrderAsync()).IsEqualTo("Safe, InBox");
        await Assert.That(await SortKeyOfAsync(folder.Id)).IsEqualTo(150);
        await Assert.That(await SortKeyOfAsync(today.Id)).IsEqualTo(160);
    }

    [Test]
    public async Task MoveCollectionDownAsync_LastCollection_ReturnsFalse()
    {
        // Arrange
        await AddAsync("InBox", 100);
        var safe = await AddAsync("Safe", 200);

        // Act
        var moved = await CreateCollectionService().MoveCollectionDownAsync(safe.Id, _databaseKey);

        // Assert
        await Assert.That(moved).IsFalse();
        await Assert.That(await TopLevelOrderAsync()).IsEqualTo("InBox, Safe");
    }

    [Test]
    public async Task MoveCollectionDownAsync_TiedSortKeys_RenumbersAndMoves()
    {
        // Arrange
        var alpha = await AddAsync("Alpha", 100);
        await AddAsync("Beta", 100);
        await AddAsync("Gamma", 100);

        // Act
        var moved = await CreateCollectionService().MoveCollectionDownAsync(alpha.Id, _databaseKey);

        // Assert
        await Assert.That(moved).IsTrue();
        await Assert.That(await TopLevelOrderAsync()).IsEqualTo("Beta, Alpha, Gamma");
    }

    [Test]
    public async Task ReorderCollectionsAsync_DropAfterTarget_MovesOnlyTopLevelCollections()
    {
        // Arrange
        var inbox = await AddAsync("InBox", 100);
        var folder = await AddAsync("Letters", 150, inbox.Id, CollectionLmType.Folder);
        var safe = await AddAsync("Safe", 200);
        await AddAsync("Overflow", 300);

        // Act
        await CreateCollectionService().ReorderCollectionsAsync([inbox.Id], safe.Id, true, _databaseKey);

        // Assert
        await Assert.That(await TopLevelOrderAsync()).IsEqualTo("Safe, InBox, Overflow");
        await Assert.That(await SortKeyOfAsync(folder.Id)).IsEqualTo(150);
    }

    [Test]
    public async Task ResequenceSortKeysAsync_LeavesVirtualCollectionsAlone()
    {
        // Arrange
        var inbox = await AddAsync("InBox", 7);
        var today = await AddAsync("Today", 3, listType: CollectionListType.Smart);

        // Act
        var count = await CreateCollectionService().ResequenceSortKeysAsync(_databaseKey);

        // Assert
        await Assert.That(count).IsEqualTo(1);
        await Assert.That(await SortKeyOfAsync(inbox.Id)).IsEqualTo(10);
        await Assert.That(await SortKeyOfAsync(today.Id)).IsEqualTo(3);
    }

    [Test]
    public async Task GetFavoriteCollectionAsync_FavoriteVirtualCollection_IsSkipped()
    {
        // Arrange
        await AddAsync("Today", 100, listType: CollectionListType.SqlBased, favorite: true);
        var safe = await AddAsync("Safe", 200, favorite: true);

        // Act
        var favorite = await CreateCollectionService().GetFavoriteCollectionAsync(_databaseKey);

        // Assert
        await Assert.That(favorite?.Id).IsEqualTo(safe.Id);
    }

    [Test]
    public async Task CreateAsync_WithParent_GoesAfterTheLastNonVirtualSibling()
    {
        // Arrange
        await AddAsync("InBox", 100);
        await AddAsync("Today", 900, lmType: CollectionLmType.Virtual);

        // Act
        var created = await CreateCollectionService().CreateAsync("Notes", null, _databaseKey);

        // Assert
        await Assert.That(created.SortKey).IsEqualTo(110);
    }

    [Test]
    public async Task GetByNameAsync_VirtualCollectionWithThatName_IsSkipped()
    {
        // Arrange
        await AddAsync("Notes", 100, listType: CollectionListType.Smart);
        var notes = await AddAsync("Notes", 200);

        // Act
        var found = await CreateCollectionService().GetByNameAsync("Notes", _databaseKey);

        // Assert
        await Assert.That(found?.Id).IsEqualTo(notes.Id);
    }
}
