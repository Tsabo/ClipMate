using ClipMate.App.Models;
using ClipMate.App.Services;
using ClipMate.App.ViewModels;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Models.Search;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data;
using ClipMate.Data.Services;
using ClipMate.Networking.SharedDatabases;
using ClipMate.Tests.Unit.Fakes;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.Extensions.Logging;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// What the search dialog's searches leave in the log, so a search that seems to do nothing can be read from it.
/// </summary>
public class SearchViewModelLoggingTests
{
    private static readonly RemoteDatabaseAddress _work = new(DeviceId.FromCertificateBytes([7]), Guid.NewGuid());
    private readonly RecordingLogger<SearchViewModel> _logger = new();
    private readonly IRemoteDatabaseService _remoteDatabaseService = A.Fake<IRemoteDatabaseService>();
    private readonly ISearchService _searchService = A.Fake<ISearchService>();
    private readonly ISearchTargetSource _targetSource = A.Fake<ISearchTargetSource>();
    private readonly SearchViewModel _viewModel;

    public SearchViewModelLoggingTests()
    {
        var databases = A.Fake<IDatabases>();
        A.CallTo(() => databases.Get("db_work")).ReturnsLazily(() => new LocalDatabase("db_work",
            new DatabaseConfiguration { Name = "Work", FilePath = typeof(SearchViewModelLoggingTests).Assembly.Location },
            A.Fake<IDatabaseContextFactory>(), _searchService));
        A.CallTo(() => databases.Get(RemoteKey)).ReturnsLazily(() => new RemoteDatabaseProxy(
            new RemoteDatabase(_work, "Work", "DESKTOP", PeerConnectionStatus.Online, null), _remoteDatabaseService));

        _viewModel = new SearchViewModel(_searchService, A.Fake<ICollectionService>(), A.Fake<IClipService>(), new StrongReferenceMessenger(),
            new SearchResultsCache(), databases, new RemoteSearchResultsCache(), _targetSource, A.Fake<ICollectionTreeBuilder>(), _logger);
    }

    private static string RemoteKey => RemoteDatabaseKey.From(_work);

    private void Target(SearchTarget target)
    {
        A.CallTo(() => _targetSource.GetSearchTarget()).Returns(target);
        _viewModel.RefreshTarget();
    }

    [Test]
    public async Task RefreshTarget_RemoteCollectionSelected_LogsTheTarget()
    {
        // Act
        Target(new SearchTarget(RemoteKey, "Work — DESKTOP") { IsRemote = true, CollectionId = Guid.NewGuid(), CollectionName = "Inbox" });

        // Assert
        var message = _logger.MessagesAt(LogLevel.Information).Single();
        await Assert.That(message).Contains(RemoteKey);
        await Assert.That(message).Contains("Work — DESKTOP");
        await Assert.That(message).Contains("Inbox");
    }

    [Test]
    public async Task SearchCommand_RemoteTarget_LogsTheSearchWithoutTheTextSearchedFor()
    {
        // Arrange
        Target(new SearchTarget(RemoteKey, "Work — DESKTOP") { IsRemote = true });
        _viewModel.SearchText = "my bank password";

        // Act
        await _viewModel.SearchCommand.ExecuteAsync(null);

        // Assert
        var started = _logger.MessagesAt(LogLevel.Information).Single(p => p.StartsWith("Searching", StringComparison.Ordinal));
        await Assert.That(started).Contains(RemoteKey);
        await Assert.That(started).Contains(nameof(SearchScope.AllCollections));
        await Assert.That(started).Contains("title");
        await Assert.That(_logger.Entries.Select(p => p.Message)).DoesNotContain(p => p.Contains("bank", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public async Task SearchCommand_RemoteTargetAnswers_LogsTheMatchCountAndTotal()
    {
        // Arrange
        Target(new SearchTarget(RemoteKey, "Work — DESKTOP") { IsRemote = true });
        var clips = new List<Clip> { new() { Id = Guid.NewGuid() }, new() { Id = Guid.NewGuid() } };
        A.CallTo(() => _remoteDatabaseService.SearchAsync(_work, A<SearchFilters>._, A<CancellationToken>._)).Returns(new SearchMatches(clips, 4212));

        // Act
        await _viewModel.SearchCommand.ExecuteAsync(null);

        // Assert
        var searched = _logger.MessagesAt(LogLevel.Information).Single(p => p.StartsWith("Searched", StringComparison.Ordinal));
        await Assert.That(searched).Contains(RemoteKey);
        await Assert.That(searched).Contains("2 of 4212");
    }

    [Test]
    public async Task CancelSearch_WhileASearchRuns_LogsWhoCanceledIt()
    {
        // Arrange
        Target(new SearchTarget(RemoteKey, "Work — DESKTOP") { IsRemote = true });
        var started = new TaskCompletionSource();
        A.CallTo(() => _remoteDatabaseService.SearchAsync(A<RemoteDatabaseAddress>._, A<SearchFilters>._, A<CancellationToken>._))
            .ReturnsLazily(async call =>
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, call.GetArgument<CancellationToken>(2));
                return new SearchMatches([], 0);
            });

        // Act
        var search = _viewModel.SearchCommand.ExecuteAsync(null);
        await started.Task;
        _viewModel.CancelSearch("the search dialog was closed");

        // Assert
        await Assert.That(async () => await search).Throws<OperationCanceledException>();
        var canceled = _logger.MessagesAt(LogLevel.Information).Single(p => p.Contains("canceled", StringComparison.Ordinal));
        await Assert.That(canceled).Contains(RemoteKey);
        await Assert.That(canceled).Contains("the search dialog was closed");
    }

    [Test]
    public async Task SearchCommand_WhileAnotherSearchRuns_LogsThatTheNewSearchCanceledTheOldOne()
    {
        // Arrange
        Target(new SearchTarget(RemoteKey, "Work — DESKTOP") { IsRemote = true });
        var started = new TaskCompletionSource();
        A.CallTo(() => _remoteDatabaseService.SearchAsync(A<RemoteDatabaseAddress>._, A<SearchFilters>._, A<CancellationToken>._))
            .ReturnsLazily(async call =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.Infinite, call.GetArgument<CancellationToken>(2));
                return new SearchMatches([], 0);
            }).Once()
            .Then.Returns(new SearchMatches([], 0));

        // Act
        var first = _viewModel.SearchCommand.ExecuteAsync(null);
        await started.Task;
        await _viewModel.SearchCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(async () => await first).Throws<OperationCanceledException>();
        var canceled = _logger.MessagesAt(LogLevel.Information).Single(p => p.Contains("canceled", StringComparison.Ordinal));
        await Assert.That(canceled).Contains("another search");
    }

    [Test]
    public async Task SearchCommand_PeerUnavailable_LogsTheFailure()
    {
        // Arrange
        Target(new SearchTarget(RemoteKey, "Work — DESKTOP") { IsRemote = true });
        A.CallTo(() => _remoteDatabaseService.SearchAsync(A<RemoteDatabaseAddress>._, A<SearchFilters>._, A<CancellationToken>._))
            .Throws(new DatabaseUnavailableException("DESKTOP is offline."));

        // Act
        await Assert.That(async () => await _viewModel.SearchCommand.ExecuteAsync(null)).Throws<DatabaseUnavailableException>();

        // Assert
        var failed = _logger.MessagesAt(LogLevel.Information).Single(p => p.Contains("failed", StringComparison.Ordinal));
        await Assert.That(failed).Contains(RemoteKey);
        await Assert.That(failed).Contains("DESKTOP is offline.");
    }

    [Test]
    public async Task SearchCommand_UnexpectedError_LogsItWithTheException()
    {
        // Arrange
        Target(new SearchTarget(RemoteKey, "Work — DESKTOP") { IsRemote = true });
        var error = new InvalidOperationException("Something broke.");
        A.CallTo(() => _remoteDatabaseService.SearchAsync(A<RemoteDatabaseAddress>._, A<SearchFilters>._, A<CancellationToken>._)).Throws(error);

        // Act
        await Assert.That(async () => await _viewModel.SearchCommand.ExecuteAsync(null)).Throws<InvalidOperationException>();

        // Assert
        var failed = _logger.Entries.Single(p => p.Level >= LogLevel.Warning);
        await Assert.That(failed.Exception).IsSameReferenceAs(error);
        await Assert.That(failed.Message).Contains(RemoteKey);
    }
}
