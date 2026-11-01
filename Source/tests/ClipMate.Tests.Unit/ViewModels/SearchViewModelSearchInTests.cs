using System.Text.Json;
using ClipMate.App.Models;
using ClipMate.App.Services;
using ClipMate.App.ViewModels;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Models.Search;
using ClipMate.Core.Services;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Tests.Unit.Fakes;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Assertions.Enums;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// The search dialog's "Search in" control: every Database with its collections and folders, defaulting to the tree
/// selection, and what follows the Database chosen in it.
/// </summary>
public class SearchViewModelSearchInTests
{
    private static readonly Guid _inboxId = Guid.NewGuid();
    private static readonly Guid _projectsId = Guid.NewGuid();
    private static readonly RemoteDatabaseAddress _work = new(DeviceId.FromCertificateBytes([7]), Guid.NewGuid());
    private static readonly RemoteDatabaseAddress _laptop = new(DeviceId.FromCertificateBytes([8]), Guid.NewGuid());
    private readonly IClipService _clipService = A.Fake<IClipService>();
    private readonly ICollectionService _collectionService = A.Fake<ICollectionService>();
    private readonly FakeDatabases _databases = new();
    private readonly ISearchService _searchService = A.Fake<ISearchService>();
    private readonly ISearchTargetSource _targetSource = A.Fake<ISearchTargetSource>();
    private readonly SearchViewModel _viewModel;

    public SearchViewModelSearchInTests()
    {
        var configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => configurationService.Configuration).Returns(new ClipMateConfiguration());
        A.CallTo(() => _collectionService.GetAllByDatabaseKeyAsync(A<string>._, A<CancellationToken>._)).Returns(new List<Collection>());
        A.CallTo(() => _targetSource.GetSearchTarget()).Returns(null);
        var treeBuilder = new CollectionTreeBuilder(configurationService, _databases, _collectionService, NullLogger<CollectionTreeBuilder>.Instance);

        _viewModel = new SearchViewModel(_searchService, _collectionService, _clipService, new StrongReferenceMessenger(), new SearchResultsCache(),
            _databases, new RemoteSearchResultsCache(), _targetSource, treeBuilder, NullLogger<SearchViewModel>.Instance);
    }

    private static string WorkKey => RemoteDatabaseKey.From(_work);

    // My Clips: Inbox with a Projects folder, Safe, and a Smart collection
    private FakeDatabase AddMyClips()
    {
        var database = _databases.AddLocal("db_mine", "My Clips");
        database.Tree =
        [
            new CollectionEntry(_inboxId, null, "Inbox", CollectionEntryKind.Collection, 1),
            new CollectionEntry(_projectsId, _inboxId, "Projects", CollectionEntryKind.Folder, 1),
            new CollectionEntry(Guid.NewGuid(), null, "Safe", CollectionEntryKind.Collection, 2),
            new CollectionEntry(Guid.NewGuid(), null, "Today", CollectionEntryKind.Smart, 3),
        ];

        return database;
    }

    // Secondary Clips: an Inbox with the same ID as My Clips' Inbox, as the seeded collections have
    private FakeDatabase AddSecondaryClips()
    {
        var database = _databases.AddLocal("db_second", "Secondary Clips");
        database.Tree = [new CollectionEntry(_inboxId, null, "Inbox", CollectionEntryKind.Collection, 1)];
        return database;
    }

    private FakeDatabase AddWork(PeerConnectionStatus status = PeerConnectionStatus.Online)
    {
        var database = _databases.AddRemote(new RemoteDatabase(_work, "Work", "DESKTOP", status, null));
        database.Tree = [new CollectionEntry(Guid.NewGuid(), null, "Reports", CollectionEntryKind.Collection, 1)];
        return database;
    }

    [Test]
    public async Task LoadSearchInOptions_LocalAndRemoteDatabases_ListsEachWithItsCollectionsAndFoldersButNoSmartCollections()
    {
        // Arrange
        AddMyClips();
        AddWork();
        _databases.AddRemote(new RemoteDatabase(_laptop, "Notes", "LAPTOP", PeerConnectionStatus.Offline, null));

        // Act
        await _viewModel.LoadSearchInOptionsAsync();

        // Assert: the Trashcan and Search Results have no clips of their own either, so they are left out
        await Assert.That(_viewModel.SearchInOptions.Select(p => $"{p.Depth}:{p.Name}"))
            .IsEquivalentTo(["0:My Clips", "1:Inbox", "2:Projects", "1:Safe", "0:Work — DESKTOP", "1:Reports", "0:Notes — LAPTOP"], CollectionOrdering.Matching);
        await Assert.That(_viewModel.SearchInOptions.Where(p => !p.IsAvailable).Select(p => p.Name)).IsEquivalentTo(["Notes — LAPTOP"]);
    }

    private SearchInOption Option(string path) => _viewModel.SearchInOptions.Single(p => p.Path == path);

    private async Task OpenOn(SearchTarget? selection)
    {
        A.CallTo(() => _targetSource.GetSearchTarget()).Returns(selection);
        _viewModel.RefreshTarget();
        await _viewModel.LoadSearchInOptionsAsync();
    }

    [Test]
    public async Task RefreshTarget_InboxOfMyClipsSelected_ChoosesMyClipsInboxRatherThanTheInboxWithTheSameIdInSecondaryClips()
    {
        // Arrange
        AddSecondaryClips();
        AddMyClips();

        // Act
        await OpenOn(new SearchTarget("db_mine", "My Clips") { CollectionId = _inboxId, CollectionName = "Inbox" });

        // Assert
        await Assert.That(_viewModel.SelectedSearchIn?.Path).IsEqualTo("My Clips › Inbox");
    }

    [Test]
    public async Task SearchCommand_AnotherDatabasesInboxChosen_SearchesThatDatabaseAndCollection()
    {
        // Arrange
        var mine = AddMyClips();
        var second = AddSecondaryClips();
        await OpenOn(new SearchTarget("db_mine", "My Clips") { CollectionId = _inboxId, CollectionName = "Inbox" });

        // Act
        _viewModel.SelectedSearchIn = Option("Secondary Clips › Inbox");
        await _viewModel.SearchCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(mine.Searches).IsEmpty();
        var filters = second.Searches.Single();
        await Assert.That(filters.Scope).IsEqualTo(SearchScope.CurrentCollection);
        await Assert.That(filters.CollectionId).IsEqualTo(_inboxId);
    }

    [Test]
    public async Task SearchCommand_FolderChosen_SearchesThatFolder()
    {
        // Arrange
        var mine = AddMyClips();
        await OpenOn(null);

        // Act
        _viewModel.SelectedSearchIn = Option("My Clips › Inbox › Projects");
        await _viewModel.SearchCommand.ExecuteAsync(null);

        // Assert
        var filters = mine.Searches.Single();
        await Assert.That(filters.Scope).IsEqualTo(SearchScope.SpecificFolder);
        await Assert.That(filters.FolderId).IsEqualTo(_projectsId);
    }

    [Test]
    public async Task SearchCommand_DatabaseChosenAfterACollection_SearchesAllItsCollections()
    {
        // Arrange
        var mine = AddMyClips();
        await OpenOn(new SearchTarget("db_mine", "My Clips") { CollectionId = _inboxId, CollectionName = "Inbox" });

        // Act
        _viewModel.SelectedSearchIn = Option("My Clips");
        await _viewModel.SearchCommand.ExecuteAsync(null);

        // Assert
        var filters = mine.Searches.Single();
        await Assert.That(filters.Scope).IsEqualTo(SearchScope.AllCollections);
        await Assert.That(filters.CollectionId).IsNull();
        await Assert.That(filters.FolderId).IsNull();
    }

    [Test]
    public async Task SelectedSearchIn_UnavailableDatabase_CannotBeChosen()
    {
        // Arrange
        AddMyClips();
        _databases.AddRemote(new RemoteDatabase(_laptop, "Notes", "LAPTOP", PeerConnectionStatus.Offline, null));
        await OpenOn(new SearchTarget("db_mine", "My Clips"));

        // Act
        _viewModel.SelectedSearchIn = Option("Notes — LAPTOP");

        // Assert
        await Assert.That(_viewModel.SelectedSearchIn?.Path).IsEqualTo("My Clips");
    }

    [Test]
    public async Task RefreshTarget_NothingSelectedInTheTree_DefaultsToTheActiveDatabase()
    {
        // Arrange
        AddMyClips();
        AddSecondaryClips();
        A.CallTo(() => _collectionService.GetActiveDatabaseKey()).Returns("db_second");

        // Act
        await OpenOn(null);

        // Assert
        await Assert.That(_viewModel.SelectedSearchIn?.Path).IsEqualTo("Secondary Clips");
    }

    [Test]
    public async Task SelectedSearchIn_RemoteDatabaseChosen_HidesTheSqlPanel()
    {
        // Arrange
        AddMyClips();
        AddWork();
        await OpenOn(new SearchTarget("db_mine", "My Clips"));
        var localSqlAvailable = _viewModel.IsSqlAvailable;

        // Act
        _viewModel.SelectedSearchIn = Option("Work — DESKTOP › Reports");

        // Assert
        await Assert.That(localSqlAvailable).IsTrue();
        await Assert.That(_viewModel.IsSqlAvailable).IsFalse();
        await Assert.That(_viewModel.Target?.DatabaseKey).IsEqualTo(WorkKey);
    }

    [Test]
    public async Task SelectedSearchIn_AnotherLocalDatabaseChosen_ListsThatDatabasesFormats()
    {
        // Arrange
        AddMyClips();
        AddSecondaryClips();
        A.CallTo(() => _clipService.GetDistinctFormatsAsync("db_mine", A<CancellationToken>._)).Returns(new List<string> { "CF_TEXT" });
        A.CallTo(() => _clipService.GetDistinctFormatsAsync("db_second", A<CancellationToken>._)).Returns(new List<string> { "HTML Format" });
        A.CallTo(() => _collectionService.GetActiveDatabaseKey()).Returns("db_mine");
        await OpenOn(new SearchTarget("db_mine", "My Clips"));
        await _viewModel.LoadFormatsCommand.ExecuteAsync(null);

        // Act
        _viewModel.SelectedSearchIn = Option("Secondary Clips › Inbox");

        // Assert
        await Assert.That(_viewModel.AvailableFormats).IsEquivalentTo(["HTML Format"]);
    }

    [Test]
    public async Task SavedQuerySelection_SavedWithAFolderScope_LeavesSearchInAsChosen()
    {
        // Arrange
        var mine = AddMyClips();
        await OpenOn(new SearchTarget("db_mine", "My Clips") { CollectionId = _inboxId, CollectionName = "Inbox" });
        var saved = new SavedSearchQuery
        {
            Name = "Projects", Query = "report",
            FiltersJson = JsonSerializer.Serialize(new SearchFilters { TitleQuery = "report", Scope = SearchScope.SpecificFolder, FolderId = Guid.NewGuid() }),
        };

        // Act
        _viewModel.SelectedSavedQuery = saved;
        await _viewModel.SearchCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(_viewModel.SelectedSearchIn?.Path).IsEqualTo("My Clips › Inbox");
        var filters = mine.Searches.Single();
        await Assert.That(filters.TitleQuery).IsEqualTo("report");
        await Assert.That(filters.Scope).IsEqualTo(SearchScope.CurrentCollection);
        await Assert.That(filters.CollectionId).IsEqualTo(_inboxId);
        await Assert.That(filters.FolderId).IsNull();
    }

    [Test]
    public async Task SelectedSearchIn_RemoteDatabaseChosen_HidesSavedSearchesThatNeedSql()
    {
        // Arrange
        AddMyClips();
        AddWork();
        A.CallTo(() => _searchService.GetSavedQueriesAsync(A<CancellationToken>._)).Returns(new List<SavedSearchQuery>
        {
            new() { Name = "Reports", Query = "report", FiltersJson = JsonSerializer.Serialize(new SearchFilters { TitleQuery = "report" }) },
            new() { Name = "Hand-written", Query = "SELECT Clips.* FROM Clips WHERE Clips.Size > 1000" },
        });
        await OpenOn(new SearchTarget("db_mine", "My Clips"));
        await _viewModel.LoadSavedQueriesCommand.ExecuteAsync(null);

        // Act
        _viewModel.SelectedSearchIn = Option("Work — DESKTOP");

        // Assert
        await Assert.That(_viewModel.SavedQueries.Select(p => p.Name)).IsEquivalentTo(["Reports"]);
    }

    [Test]
    public async Task LoadSearchInOptions_PeerSlowToSendItsTree_ListsTheOtherDatabasesWithoutWaiting()
    {
        // Arrange
        AddMyClips();
        var tree = new TaskCompletionSource();
        AddWork().TreeReady = tree.Task;
        A.CallTo(() => _targetSource.GetSearchTarget()).Returns(new SearchTarget("db_mine", "My Clips") { CollectionId = _inboxId, CollectionName = "Inbox" });
        _viewModel.RefreshTarget();

        // Act
        var loading = _viewModel.LoadSearchInOptionsAsync();
        var whileWaiting = _viewModel.SearchInOptions.Select(p => p.Path).ToList();
        var selectedWhileWaiting = _viewModel.SelectedSearchIn?.Path;
        tree.SetResult();
        await loading;

        // Assert
        await Assert.That(whileWaiting).IsEquivalentTo(["My Clips", "My Clips › Inbox", "My Clips › Inbox › Projects", "My Clips › Safe", "Work — DESKTOP"],
            CollectionOrdering.Matching);
        await Assert.That(selectedWhileWaiting).IsEqualTo("My Clips › Inbox");
        await Assert.That(_viewModel.SearchInOptions.Select(p => p.Path)).Contains("Work — DESKTOP › Reports");
    }

    [Test]
    public async Task LoadSearchInOptions_PeerGoesOfflineBeforeSendingItsTree_GraysItOut()
    {
        // Arrange
        AddMyClips();
        var work = AddWork();
        var tree = new TaskCompletionSource();
        work.TreeReady = tree.Task;

        // Act
        var loading = _viewModel.LoadSearchInOptionsAsync();
        work.IsAvailable = false;
        tree.SetResult();
        await loading;

        // Assert: not an error, and it can't be chosen
        await Assert.That(Option("Work — DESKTOP").IsAvailable).IsFalse();
    }

    [Test]
    public async Task SearchCommand_WhileThePeerSearches_SaysItIsSearchingThatDatabase()
    {
        // Arrange
        AddMyClips();
        var work = AddWork();
        var answer = new TaskCompletionSource();
        work.SearchReady = answer.Task;
        await OpenOn(null);
        _viewModel.SelectedSearchIn = Option("Work — DESKTOP");

        // Act
        var search = _viewModel.SearchCommand.ExecuteAsync(null);
        var searching = _viewModel.IsSearching;
        var description = _viewModel.SearchingDescription;
        answer.SetResult();
        await search;

        // Assert
        await Assert.That(searching).IsTrue();
        await Assert.That(description).IsEqualTo("Searching Work — DESKTOP…");
        await Assert.That(_viewModel.IsSearching).IsFalse();
    }
}
