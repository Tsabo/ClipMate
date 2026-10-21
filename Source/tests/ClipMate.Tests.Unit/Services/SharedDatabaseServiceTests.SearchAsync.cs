using ClipMate.Core.Models;
using ClipMate.Core.Models.Search;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

public partial class SharedDatabaseServiceTests
{
    [Test]
    public async Task SearchAsync_SharedDatabase_RunsTheFiltersOnThatDatabaseCappedAtTheRemoteSearchLimit()
    {
        // Arrange
        AddDatabase("db_work", "Work", _workId, true);
        var matches = new SearchMatches([new Clip { Id = Guid.NewGuid(), Title = "report" }], 4212);
        A.CallTo(() => _searchService.SearchDatabaseAsync("db_work", A<SearchFilters>._, 1000, A<CancellationToken>._)).Returns(matches);

        // Act
        var result = await _service.SearchAsync(_workId, new SearchFilters { TitleQuery = "report" });

        // Assert
        await Assert.That(result).IsSameReferenceAs(matches);
        A.CallTo(() => _searchService.SearchDatabaseAsync("db_work", A<SearchFilters>.That.Matches(p => p.TitleQuery == "report"), 1000,
            A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task SearchAsync_AskingForDeletedClipsOrShortcuts_NeverServesThem()
    {
        // Arrange: deleted clips and shortcuts are not served to Peers
        AddDatabase("db_work", "Work", _workId, true);

        // Act
        await _service.SearchAsync(_workId, new SearchFilters { TitleQuery = "report", IncludeDeleted = true, HasShortcutOnly = true });

        // Assert
        A.CallTo(() => _searchService.SearchDatabaseAsync("db_work",
            A<SearchFilters>.That.Matches(p => !p.IncludeDeleted && p.HasShortcutOnly == null && p.TitleQuery == "report"), A<int?>._,
            A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task SearchAsync_DatabaseNotShared_ReturnsNullWithoutSearching()
    {
        // Arrange
        AddDatabase("db_home", "Home", _homeId, false);

        // Act
        var result = await _service.SearchAsync(_homeId, new SearchFilters { TitleQuery = "report" });

        // Assert
        await Assert.That(result).IsNull();
        A.CallTo(_searchService).MustNotHaveHappened();
    }

    [Test]
    public async Task SearchAsync_CurrentCollectionOfThatDatabase_SearchesOnlyThatCollection()
    {
        // Arrange
        AddDatabase("db_work", "Work", _workId, true);
        var inbox = AddCollection("db_work", new Collection { Id = Guid.NewGuid(), Title = "Inbox" });

        // Act
        await _service.SearchAsync(_workId, new SearchFilters { Scope = SearchScope.CurrentCollection, CollectionId = inbox.Id });

        // Assert
        A.CallTo(() => _searchService.SearchDatabaseAsync("db_work",
            A<SearchFilters>.That.Matches(p => p.Scope == SearchScope.CurrentCollection && p.CollectionId == inbox.Id), A<int?>._,
            A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task SearchAsync_SpecificFolderOfThatDatabase_SearchesOnlyThatFolder()
    {
        // Arrange
        AddDatabase("db_work", "Work", _workId, true);
        var inbox = AddCollection("db_work", new Collection { Id = Guid.NewGuid(), Title = "Inbox" });
        var projects = AddCollection("db_work", new Collection { Id = Guid.NewGuid(), ParentId = inbox.Id, Title = "Projects", LmType = CollectionLmType.Folder });

        // Act
        await _service.SearchAsync(_workId, new SearchFilters { Scope = SearchScope.SpecificFolder, FolderId = projects.Id });

        // Assert
        A.CallTo(() => _searchService.SearchDatabaseAsync("db_work",
            A<SearchFilters>.That.Matches(p => p.Scope == SearchScope.SpecificFolder && p.FolderId == projects.Id), A<int?>._,
            A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    [Arguments(SearchScope.CurrentCollection)]
    [Arguments(SearchScope.SpecificFolder)]
    public async Task SearchAsync_CollectionOrFolderNotInThatDatabase_ReturnsNullWithoutSearching(SearchScope scope)
    {
        // Arrange: an ID from another Database (shared or not) is unknown here
        AddDatabase("db_work", "Work", _workId, true);
        AddCollection("db_work", new Collection { Id = Guid.NewGuid(), Title = "Inbox" });
        var elsewhere = Guid.NewGuid();

        // Act
        var result = await _service.SearchAsync(_workId, new SearchFilters { Scope = scope, CollectionId = elsewhere, FolderId = elsewhere });

        // Assert
        await Assert.That(result).IsNull();
        A.CallTo(_searchService).MustNotHaveHappened();
    }

    [Test]
    [Arguments(SearchScope.CurrentCollection)]
    [Arguments(SearchScope.SpecificFolder)]
    public async Task SearchAsync_ScopeWithoutItsCollectionOrFolder_ThrowsArgumentException(SearchScope scope)
    {
        // Arrange
        AddDatabase("db_work", "Work", _workId, true);

        // Act / Assert
        await Assert.That(async () => await _service.SearchAsync(_workId, new SearchFilters { Scope = scope })).Throws<ArgumentException>();
        A.CallTo(_searchService).MustNotHaveHappened();
    }
}
