using System.Text.Json;
using ClipMate.App.Models;
using ClipMate.App.Services;
using ClipMate.App.ViewModels;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Models.Search;
using ClipMate.Core.Services;
using ClipMate.Core.Services.Abstractions;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// The search dialog's target and scope, and Remote Search through it.
/// </summary>
public class SearchViewModelRemoteSearchTests
{
    private static readonly RemoteDatabaseAddress _work = new(DeviceId.FromCertificateBytes([7]), Guid.NewGuid());
    private readonly List<SearchExecutedEvent> _executed = [];
    private readonly IMessenger _messenger = new StrongReferenceMessenger();
    private readonly IRemoteDatabaseService _remoteDatabaseService = A.Fake<IRemoteDatabaseService>();
    private readonly RemoteSearchResultsCache _remoteSearchResultsCache = new();
    private readonly ISearchService _searchService = A.Fake<ISearchService>();
    private readonly ISearchTargetSource _targetSource = A.Fake<ISearchTargetSource>();
    private readonly SearchViewModel _viewModel;

    public SearchViewModelRemoteSearchTests()
    {
        _messenger.Register<SearchViewModelRemoteSearchTests, SearchExecutedEvent>(this, (r, m) => r._executed.Add(m));
        _viewModel = new SearchViewModel(_searchService, A.Fake<ICollectionService>(), A.Fake<IClipService>(), _messenger, new SearchResultsCache(),
            _remoteDatabaseService, _remoteSearchResultsCache, _targetSource);
    }

    private static string RemoteKey => RemoteDatabaseKey.From(_work);

    private void Target(SearchTarget target)
    {
        A.CallTo(() => _targetSource.GetSearchTarget()).Returns(target);
        _viewModel.RefreshTarget();
    }

    [Test]
    public async Task RefreshTarget_FolderSelected_OffersAllThreeScopesAndDefaultsToAllCollections()
    {
        // Arrange: a scope picked the last time the dialog was open
        _viewModel.SearchScope = SearchScope.SpecificFolder;

        // Act
        Target(new SearchTarget("db_work", "Work") { CollectionId = Guid.NewGuid(), CollectionName = "Inbox", FolderId = Guid.NewGuid(), FolderName = "Projects" });

        // Assert
        await Assert.That(_viewModel.AvailableScopes.Select(p => p.Scope))
            .IsEquivalentTo([SearchScope.AllCollections, SearchScope.CurrentCollection, SearchScope.SpecificFolder]);
        await Assert.That(_viewModel.AvailableScopes.Select(p => p.Label)).Contains("Folder: Projects");
        await Assert.That(_viewModel.SearchScope).IsEqualTo(SearchScope.AllCollections);
    }

    [Test]
    public async Task RefreshTarget_RemoteCollectionSelected_OffersItAndDefaultsToAllCollections()
    {
        // Act
        Target(new SearchTarget(RemoteKey, "Work — DESKTOP") { CollectionId = Guid.NewGuid(), CollectionName = "Inbox" });

        // Assert
        await Assert.That(_viewModel.AvailableScopes.Select(p => p.Scope)).IsEquivalentTo([SearchScope.AllCollections, SearchScope.CurrentCollection]);
        await Assert.That(_viewModel.SearchScope).IsEqualTo(SearchScope.AllCollections);
    }

    [Test]
    public async Task SearchCommand_LocalTargetWithTheDefaultScope_SearchesAllCollectionsOfTheSelectedDatabase()
    {
        // Arrange
        Target(new SearchTarget("db_work", "Work") { CollectionId = Guid.NewGuid(), CollectionName = "Inbox" });
        A.CallTo(() => _searchService.SearchDatabaseAsync("db_work", A<SearchFilters>._, A<int?>._, A<CancellationToken>._))
            .Returns(new SearchMatches([], 0));

        // Act
        await _viewModel.SearchCommand.ExecuteAsync(null);

        // Assert
        A.CallTo(() => _searchService.SearchDatabaseAsync("db_work",
            A<SearchFilters>.That.Matches(p => p.Scope == SearchScope.AllCollections && p.CollectionId == null && p.FolderId == null), null,
            A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task RefreshTarget_NoCollectionSelected_SearchesAllCollections()
    {
        // Arrange: e.g. a Smart collection or the Remote Database node itself
        _viewModel.SearchScope = SearchScope.SpecificFolder;

        // Act
        Target(new SearchTarget(RemoteKey, "Work — DESKTOP"));

        // Assert
        await Assert.That(_viewModel.AvailableScopes.Select(p => p.Scope)).IsEquivalentTo([SearchScope.AllCollections]);
        await Assert.That(_viewModel.SearchScope).IsEqualTo(SearchScope.AllCollections);
    }

    [Test]
    public async Task RefreshTarget_RemoteDatabase_HidesTheSqlPanel()
    {
        // Act
        Target(new SearchTarget(RemoteKey, "Work — DESKTOP"));
        var remoteSqlAvailable = _viewModel.IsSqlAvailable;
        Target(new SearchTarget("db_work", "Work"));

        // Assert
        await Assert.That(remoteSqlAvailable).IsFalse();
        await Assert.That(_viewModel.IsSqlAvailable).IsTrue();
    }

    [Test]
    public async Task SearchCommand_RemoteTarget_AsksThePeerAndCachesAndAnnouncesTheResults()
    {
        // Arrange
        var inboxId = Guid.NewGuid();
        Target(new SearchTarget(RemoteKey, "Work — DESKTOP") { CollectionId = inboxId, CollectionName = "Inbox" });
        _viewModel.SearchScope = SearchScope.CurrentCollection;
        var clips = new List<Clip> { new() { Id = Guid.NewGuid(), Title = "report 1" }, new() { Id = Guid.NewGuid(), Title = "report 2" } };
        A.CallTo(() => _remoteDatabaseService.SearchAsync(_work, A<SearchFilters>._, A<CancellationToken>._)).Returns(new SearchMatches(clips, 4212));
        _viewModel.SearchText = "report";

        // Act
        await _viewModel.SearchCommand.ExecuteAsync(null);

        // Assert
        A.CallTo(() => _remoteDatabaseService.SearchAsync(_work,
            A<SearchFilters>.That.Matches(p => p.TitleQuery == "report" && p.Scope == SearchScope.CurrentCollection && p.CollectionId == inboxId),
            A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(_searchService).Where(p => p.Method.Name.StartsWith("Search")).MustNotHaveHappened();

        var cached = _remoteSearchResultsCache.GetResults(_work);
        await Assert.That(cached).IsNotNull();
        await Assert.That(cached!.Clips.Count).IsEqualTo(2);
        await Assert.That(cached.TotalMatches).IsEqualTo(4212);
        await Assert.That(_viewModel.TotalMatches).IsEqualTo(4212);
        await Assert.That(_executed.Single().DatabaseKey).IsEqualTo(RemoteKey);
    }

    [Test]
    public async Task SearchCommand_RemoteTarget_NeverAsksForDeletedClipsOrShortcuts()
    {
        // Arrange
        Target(new SearchTarget(RemoteKey, "Work — DESKTOP"));
        _viewModel.IncludeDeletedClips = true;
        _viewModel.HasShortcutOnly = true;

        // Act
        await _viewModel.SearchCommand.ExecuteAsync(null);

        // Assert
        A.CallTo(() => _remoteDatabaseService.SearchAsync(_work, A<SearchFilters>.That.Matches(p => !p.IncludeDeleted && p.HasShortcutOnly == null),
            A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task SearchCommand_PeerUnavailable_LeavesTheCacheAloneAndReportsTheError()
    {
        // Arrange
        Target(new SearchTarget(RemoteKey, "Work — DESKTOP"));
        A.CallTo(() => _remoteDatabaseService.SearchAsync(A<RemoteDatabaseAddress>._, A<SearchFilters>._, A<CancellationToken>._))
            .Throws(new RemoteDatabaseUnavailableException("DESKTOP is offline."));

        // Act / Assert
        await Assert.That(async () => await _viewModel.SearchCommand.ExecuteAsync(null)).Throws<RemoteDatabaseUnavailableException>();
        await Assert.That(_remoteSearchResultsCache.GetResults(_work)).IsNull();
        await Assert.That(_executed).IsEmpty();
        await Assert.That(_viewModel.IsSearching).IsFalse();
    }

    [Test]
    public async Task CancelSearch_WhileARemoteSearchRuns_CancelsItsToken()
    {
        // Arrange
        Target(new SearchTarget(RemoteKey, "Work — DESKTOP"));
        var token = CancellationToken.None;
        var started = new TaskCompletionSource();
        A.CallTo(() => _remoteDatabaseService.SearchAsync(A<RemoteDatabaseAddress>._, A<SearchFilters>._, A<CancellationToken>._))
            .ReturnsLazily(async call =>
            {
                token = call.GetArgument<CancellationToken>(2);
                started.SetResult();
                await Task.Delay(Timeout.Infinite, token);
                return new SearchMatches([], 0);
            });

        // Act
        var search = _viewModel.SearchCommand.ExecuteAsync(null);
        await started.Task;
        _viewModel.CancelSearch();

        // Assert
        await Assert.That(async () => await search).Throws<OperationCanceledException>();
        await Assert.That(token.IsCancellationRequested).IsTrue();
        await Assert.That(_executed).IsEmpty();
    }

    [Test]
    public async Task SearchCommand_LocalTargetWithFolderScope_SearchesThatDatabaseAndFolder()
    {
        // Arrange
        var folderId = Guid.NewGuid();
        Target(new SearchTarget("db_work", "Work") { CollectionId = Guid.NewGuid(), CollectionName = "Inbox", FolderId = folderId, FolderName = "Projects" });
        _viewModel.SearchScope = SearchScope.SpecificFolder;
        A.CallTo(() => _searchService.SearchDatabaseAsync("db_work", A<SearchFilters>._, A<int?>._, A<CancellationToken>._))
            .Returns(new SearchMatches([new Clip { Id = Guid.NewGuid() }], 1));

        // Act
        await _viewModel.SearchCommand.ExecuteAsync(null);

        // Assert
        A.CallTo(() => _searchService.SearchDatabaseAsync("db_work",
            A<SearchFilters>.That.Matches(p => p.Scope == SearchScope.SpecificFolder && p.FolderId == folderId), null,
            A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(_remoteDatabaseService).MustNotHaveHappened();
        await Assert.That(_executed.Single().DatabaseKey).IsEqualTo("db_work");
    }

    [Test]
    public async Task LoadSavedQueries_RemoteTarget_LeavesOutSearchesThatDependOnSql()
    {
        // Arrange
        A.CallTo(() => _searchService.GetSavedQueriesAsync(A<CancellationToken>._)).Returns(new List<SavedSearchQuery>
        {
            new() { Name = "Reports", Query = "report", FiltersJson = JsonSerializer.Serialize(new SearchFilters { TitleQuery = "report" }) },
            new() { Name = "Hand-written", Query = "SELECT Clips.* FROM Clips WHERE Clips.Size > 1000" },
        });
        Target(new SearchTarget(RemoteKey, "Work — DESKTOP"));

        // Act
        await _viewModel.LoadSavedQueriesCommand.ExecuteAsync(null);
        var remote = _viewModel.SavedQueries.Select(p => p.Name).ToList();
        Target(new SearchTarget("db_work", "Work"));
        await _viewModel.LoadSavedQueriesCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(remote).IsEquivalentTo(["Reports"]);
        await Assert.That(_viewModel.SavedQueries.Select(p => p.Name)).IsEquivalentTo(["Reports", "Hand-written"]);
    }

    [Test]
    public async Task SavedQuerySelection_ScopeTheTargetCannotOffer_FallsBackToAllCollections()
    {
        // Arrange: saved while a folder was selected; now the Remote Database node itself is
        Target(new SearchTarget(RemoteKey, "Work — DESKTOP"));
        var saved = new SavedSearchQuery
        {
            Name = "Projects", Query = "report",
            FiltersJson = JsonSerializer.Serialize(new SearchFilters { TitleQuery = "report", Scope = SearchScope.SpecificFolder, FolderId = Guid.NewGuid() }),
        };

        // Act
        _viewModel.SelectedSavedQuery = saved;
        await _viewModel.SearchCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(_viewModel.SearchScope).IsEqualTo(SearchScope.AllCollections);
        A.CallTo(() => _remoteDatabaseService.SearchAsync(_work,
            A<SearchFilters>.That.Matches(p => p.Scope == SearchScope.AllCollections && p.FolderId == null && p.TitleQuery == "report"),
            A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }
}
