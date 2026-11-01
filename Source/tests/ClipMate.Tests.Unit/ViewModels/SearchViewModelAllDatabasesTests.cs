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
using ClipMate.Core.ValueObjects;
using ClipMate.Tests.Unit.Fakes;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// "All Databases" in the search dialog: one search of every available Database, local and remote, each one's matches
/// in its own Search Results node, with the Databases that couldn't be searched skipped and reported.
/// </summary>
public class SearchViewModelAllDatabasesTests
{
    private static readonly RemoteDatabaseAddress _work = new(DeviceId.FromCertificateBytes([7]), Guid.NewGuid());
    private static readonly RemoteDatabaseAddress _laptop = new(DeviceId.FromCertificateBytes([8]), Guid.NewGuid());
    private readonly ICollectionService _collectionService = A.Fake<ICollectionService>();
    private readonly FakeDatabases _databases = new();
    private readonly List<SearchExecutedEvent> _executed = [];
    private readonly IMessenger _messenger = new StrongReferenceMessenger();
    private readonly RemoteSearchResultsCache _remoteSearchResultsCache = new();
    private readonly SearchResultsCache _searchResultsCache = new();
    private readonly ISearchService _searchService = A.Fake<ISearchService>();
    private readonly List<string> _statuses = [];
    private readonly ISearchTargetSource _targetSource = A.Fake<ISearchTargetSource>();
    private readonly SearchViewModel _viewModel;

    public SearchViewModelAllDatabasesTests()
    {
        var configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => configurationService.Configuration).Returns(new ClipMateConfiguration());
        A.CallTo(() => _collectionService.GetAllByDatabaseKeyAsync(A<string>._, A<CancellationToken>._)).Returns(new List<Collection>());
        A.CallTo(() => _targetSource.GetSearchTarget()).Returns(null);
        _messenger.Register<SearchViewModelAllDatabasesTests, SearchExecutedEvent>(this, (r, m) => r._executed.Add(m));
        _messenger.Register<SearchViewModelAllDatabasesTests, StatusUpdateEvent>(this, (r, m) => r._statuses.Add(m.Message));
        var treeBuilder = new CollectionTreeBuilder(configurationService, _databases, _collectionService, NullLogger<CollectionTreeBuilder>.Instance);

        _viewModel = new SearchViewModel(_searchService, _collectionService, A.Fake<IClipService>(), _messenger, _searchResultsCache, _databases,
            _remoteSearchResultsCache, _targetSource, treeBuilder, NullLogger<SearchViewModel>.Instance);
    }

    private FakeDatabase AddMyClips() => _databases.AddLocal("db_mine", "My Clips");

    private FakeDatabase AddSecondaryClips() => _databases.AddLocal("db_second", "Secondary Clips");

    private FakeDatabase AddWork(PeerConnectionStatus status = PeerConnectionStatus.Online) =>
        _databases.AddRemote(new RemoteDatabase(_work, "Work", "DESKTOP", status, null));

    private FakeDatabase AddLaptop(PeerConnectionStatus status = PeerConnectionStatus.Offline) =>
        _databases.AddRemote(new RemoteDatabase(_laptop, "Notes", "LAPTOP", status, null));

    private static Clip NewClip(string title) => new() { Id = Guid.NewGuid(), Title = title, Type = ClipType.Text, CapturedAt = DateTimeOffset.Now };

    private static SearchMatches Matches(params Clip[] clips) => new(clips, clips.Length);

    private async Task OpenOnAllDatabases()
    {
        _viewModel.RefreshTarget();
        await _viewModel.LoadSearchInOptionsAsync();
        _viewModel.SelectedSearchIn = _viewModel.SearchInOptions.Single(p => p.Path == "All Databases");
    }

    [Test]
    public async Task LoadSearchInOptions_SomeDatabases_ListsAllDatabasesFirstWithAnIconOfItsOwn()
    {
        // Arrange
        AddMyClips();
        AddWork();

        // Act
        await _viewModel.LoadSearchInOptionsAsync();

        // Assert
        var first = _viewModel.SearchInOptions[0];
        await Assert.That(first.Path).IsEqualTo("All Databases");
        await Assert.That(first.IsAvailable).IsTrue();
        await Assert.That(_viewModel.SearchInOptions.Skip(1).Select(p => p.Icon)).DoesNotContain(first.Icon);
    }

    [Test]
    public async Task SelectedSearchIn_AllDatabases_HidesTheSqlPanelAndKeepsTheDeletedAndShortcutFiltersWithANote()
    {
        // Arrange
        AddMyClips();
        AddWork();
        A.CallTo(() => _searchService.GetSavedQueriesAsync(A<CancellationToken>._)).Returns(new List<SavedSearchQuery>
        {
            new() { Name = "Reports", Query = "report", FiltersJson = "{\"TitleQuery\":\"report\"}" },
            new() { Name = "Hand-written", Query = "SELECT Clips.* FROM Clips WHERE Clips.Size > 1000" },
        });

        // Act
        await OpenOnAllDatabases();
        await _viewModel.LoadSavedQueriesCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(_viewModel.IsSqlAvailable).IsFalse();
        await Assert.That(_viewModel.SavedQueries.Select(p => p.Name)).IsEquivalentTo(["Reports"]);
        await Assert.That(_viewModel.CanSearchDeletedAndShortcuts).IsTrue();
        await Assert.That(_viewModel.ShowsPeerFiltersNote).IsTrue();
    }

    [Test]
    public async Task SearchCommand_AllDatabases_FillsEachDatabasesSearchResultsAndSelectsTheFirstWithMatchesOnce()
    {
        // Arrange
        var mine = AddMyClips();
        var second = AddSecondaryClips();
        var work = AddWork();
        _searchResultsCache.SetResults("db_mine", new SearchResult("older", [Guid.NewGuid()]));
        second.SearchResults = Matches(NewClip("Report 1"), NewClip("Report 2"));
        work.SearchResults = Matches(NewClip("Report 3"));
        await OpenOnAllDatabases();
        _viewModel.SearchText = "report";

        // Act
        await _viewModel.SearchCommand.ExecuteAsync(null);

        // Assert: one search each, in parallel
        await Assert.That(mine.Searches.Count + second.Searches.Count + work.Searches.Count).IsEqualTo(3);

        // My Clips found nothing, so its node no longer shows the older query's matches
        await Assert.That(_searchResultsCache.GetResults("db_mine")?.ClipIds).IsEmpty();
        await Assert.That(_searchResultsCache.GetResults("db_second")?.ClipIds).IsEquivalentTo(second.SearchResults.Clips.Select(p => p.Id));
        await Assert.That(_remoteSearchResultsCache.GetResults(_work)?.Clips.Select(p => p.Title ?? string.Empty)).IsEquivalentTo(["Report 3"]);

        // Each node is filled without being selected; then the first Database in tree order with matches is selected
        await Assert.That(_executed.Where(p => !p.SelectsNode).Select(p => p.DatabaseKey))
            .IsEquivalentTo(["db_mine", "db_second", RemoteDatabaseKey.From(_work)]);
        await Assert.That(_executed.Where(p => p.SelectsNode).Select(p => p.DatabaseKey)).IsEquivalentTo(["db_second"]);
        await Assert.That(_executed[^1].SelectsNode).IsTrue();
        await Assert.That(_statuses).IsEquivalentTo(["3 matches in 3 Databases"]);
        await Assert.That(_viewModel.IsSearching).IsFalse();
    }

    [Test]
    public async Task SearchCommand_AllDatabasesNothingMatches_SelectsNoNode()
    {
        // Arrange
        AddMyClips();
        AddWork();
        await OpenOnAllDatabases();

        // Act
        await _viewModel.SearchCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(_executed.Where(p => p.SelectsNode)).IsEmpty();
        await Assert.That(_statuses).IsEquivalentTo(["0 matches in 2 Databases"]);
    }

    [Test]
    public async Task SearchCommand_AllDatabasesOnePeerOfflineAndOneFailing_SkipsThemAndSaysWhy()
    {
        // Arrange
        var mine = AddMyClips();
        mine.SearchResults = Matches(NewClip("Report 1"), NewClip("Report 2"));
        var work = AddWork();
        work.SearchReady = Task.FromException(new DatabaseUnavailableException("DESKTOP cannot be reached."));
        var laptop = AddLaptop();
        var olderResult = new RemoteSearchResult("older", [NewClip("Older")], 1);
        _remoteSearchResultsCache.SetResults(_work, olderResult);
        await OpenOnAllDatabases();

        // Act
        await _viewModel.SearchCommand.ExecuteAsync(null);

        // Assert: the search doesn't fail; the skipped Databases' nodes are left as they were
        await Assert.That(laptop.Searches).IsEmpty();
        await Assert.That(_remoteSearchResultsCache.GetResults(_work)).IsSameReferenceAs(olderResult);
        await Assert.That(_executed.Select(p => p.DatabaseKey).Distinct()).IsEquivalentTo(["db_mine"]);
        await Assert.That(_statuses).IsEquivalentTo(["2 matches in 1 Database. 2 couldn't be searched: DESKTOP cannot be reached; LAPTOP is offline"]);
    }

    [Test]
    public async Task SearchCommand_AllDatabasesWithDeletedAndShortcutFilters_AppliesThemToLocalDatabasesOnly()
    {
        // Arrange
        var mine = AddMyClips();
        var work = AddWork();
        await OpenOnAllDatabases();
        _viewModel.IncludeDeletedClips = true;
        _viewModel.HasShortcutOnly = true;

        // Act
        await _viewModel.SearchCommand.ExecuteAsync(null);

        // Assert
        var local = mine.Searches.Single();
        await Assert.That(local.IncludeDeleted).IsTrue();
        await Assert.That(local.HasShortcutOnly).IsTrue();
        await Assert.That(local.Scope).IsEqualTo(SearchScope.AllCollections);
        var remote = work.Searches.Single();
        await Assert.That(remote.IncludeDeleted).IsFalse();
        await Assert.That(remote.HasShortcutOnly).IsNull();
    }

    [Test]
    public async Task SearchCommand_AllDatabasesWhileAPeerIsSlow_SaysHowManyAreDoneAndWhichAreStillSearching()
    {
        // Arrange
        AddMyClips();
        AddSecondaryClips();
        var work = AddWork();
        var answer = new TaskCompletionSource();
        work.SearchReady = answer.Task;
        await OpenOnAllDatabases();

        // Act
        var search = _viewModel.SearchCommand.ExecuteAsync(null);
        var whileWaiting = _viewModel.SearchingDescription;
        var executedWhileWaiting = _executed.Count;
        answer.SetResult();
        await search;

        // Assert: the local Databases' nodes are filled at once
        await Assert.That(whileWaiting).IsEqualTo("Searched 2 of 3 Databases… still searching Work — DESKTOP");
        await Assert.That(executedWhileWaiting).IsEqualTo(2);
    }

    [Test]
    public async Task CancelSearch_AllDatabasesWhileAPeerIsSlow_StopsTheSearchWithoutSelectingOrReporting()
    {
        // Arrange
        AddMyClips().SearchResults = Matches(NewClip("Report 1"));
        var work = AddWork();
        work.SearchReady = new TaskCompletionSource().Task;
        await OpenOnAllDatabases();
        var search = _viewModel.SearchCommand.ExecuteAsync(null);

        // Act
        _viewModel.CancelSearch("Cancel was pressed");

        // Assert
        await Assert.That(async () => await search).Throws<OperationCanceledException>();
        await Assert.That(_viewModel.IsSearching).IsFalse();
        await Assert.That(_executed.Where(p => p.SelectsNode)).IsEmpty();
        await Assert.That(_statuses).IsEmpty();
    }

    [Test]
    public async Task CancelSearch_AllDatabasesWithAPeerThatFailsAfterwards_LeavesNoFailureUnobserved()
    {
        // Arrange: My Clips stops when canceled; the Peer's call fails only later
        AddMyClips().SearchReady = new TaskCompletionSource().Task;
        var work = AddWork();
        var peerAnswer = new TaskCompletionSource();
        work.SearchReady = peerAnswer.Task;
        work.SearchIgnoresCancellation = true;
        var lateFailure = new DatabaseUnavailableException("DESKTOP dropped the call.");
        var unobserved = new List<Exception>();
        EventHandler<UnobservedTaskExceptionEventArgs> recordUnobserved = (_, e) =>
        {
            if (!e.Exception.InnerExceptions.Contains(lateFailure))
                return;

            lock (unobserved)
                unobserved.Add(e.Exception);
        };

        TaskScheduler.UnobservedTaskException += recordUnobserved;
        try
        {
            await OpenOnAllDatabases();
            await StartAndCancelSearch();

            // Act
            peerAnswer.SetException(lateFailure);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Assert
            await Assert.That(unobserved).IsEmpty();
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= recordUnobserved;
        }
    }

    // Kept out of the test so nothing there holds the canceled search's tasks
    private async Task StartAndCancelSearch()
    {
        var search = _viewModel.SearchCommand.ExecuteAsync(null);
        _viewModel.CancelSearch("Cancel was pressed");
        await Assert.That(async () => await search).Throws<OperationCanceledException>();
    }
}
