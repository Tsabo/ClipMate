using System.Text.Json;
using ClipMate.App.Services;
using ClipMate.App.ViewModels;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Search;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Tests for SearchViewModel
/// </summary>
public class SearchViewModelTests
{
    private readonly IClipService _mockClipService;
    private readonly ICollectionService _mockCollectionService;
    private readonly IMessenger _mockMessenger;
    private readonly ISearchService _mockSearchService;
    private readonly SearchResultsCache _searchResultsCache;
    private readonly SearchViewModel _viewModel;

    public SearchViewModelTests()
    {
        _mockSearchService = A.Fake<ISearchService>();
        _mockCollectionService = A.Fake<ICollectionService>();
        _mockClipService = A.Fake<IClipService>();
        _mockMessenger = A.Fake<IMessenger>();
        _searchResultsCache = new SearchResultsCache();

        // Setup mock collection service to return a database key
        A.CallTo(() => _mockCollectionService.GetActiveDatabaseKey()).Returns("test_database");

        _viewModel = new SearchViewModel(
            _mockSearchService,
            _mockCollectionService,
            _mockClipService,
            _mockMessenger,
            _searchResultsCache,
            A.Fake<IRemoteDatabaseService>(),
            new RemoteSearchResultsCache(),
            A.Fake<ISearchTargetSource>());
    }

    [Test]
    public async Task Constructor_WithNullSearchService_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => new SearchViewModel(
                null!,
                _mockCollectionService,
                _mockClipService,
                _mockMessenger,
                _searchResultsCache,
                A.Fake<IRemoteDatabaseService>(),
                new RemoteSearchResultsCache(),
                A.Fake<ISearchTargetSource>()))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task SearchText_WhenSet_ShouldRaisePropertyChanged()
    {
        // Arrange
        var propertyChangedRaised = false;
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.SearchText))
                propertyChangedRaised = true;
        };

        // Act
        _viewModel.SearchText = "test query";

        // Assert
        await Assert.That(propertyChangedRaised).IsTrue();
        await Assert.That(_viewModel.SearchText).IsEqualTo("test query");
    }

    [Test]
    public async Task SearchCommand_WithValidQuery_ShouldReturnResults()
    {
        // Arrange
        var clips = new List<Clip>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TextContent = "Test result",
                Type = ClipType.Text,
                CapturedAt = DateTime.UtcNow,
            },
        };

        var searchResults = new SearchResults
        {
            Clips = clips,
            TotalMatches = 1,
            Query = "test",
        };

        A.CallTo(() => _mockSearchService.SearchAsync(A<string>.Ignored, A<SearchFilters>.Ignored, A<CancellationToken>.Ignored))
            .Returns(searchResults);

        _viewModel.SearchText = "test";

        // Act
        await _viewModel.SearchCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(_viewModel.SearchResults.Count).IsEqualTo(1);
        await Assert.That(_viewModel.TotalMatches).IsEqualTo(1);
        // ViewModel calls SearchAsync("", filters) where SearchText is in filters.TitleQuery
        A.CallTo(() => _mockSearchService.SearchAsync("", A<SearchFilters>.Ignored, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task SearchCommand_WithEmptyQuery_ShouldClearResults()
    {
        // Arrange
        _viewModel.SearchText = "";

        // Mock empty search results
        var searchResults = new SearchResults
        {
            Clips = [],
            TotalMatches = 0,
            Query = "",
        };

        A.CallTo(() => _mockSearchService.SearchAsync(A<string>.Ignored, A<SearchFilters>.Ignored, A<CancellationToken>.Ignored))
            .Returns(searchResults);

        // Act
        await _viewModel.SearchCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(_viewModel.SearchResults.Count).IsEqualTo(0);
        await Assert.That(_viewModel.TotalMatches).IsEqualTo(0);
        // Even with empty text, SearchAsync is still called with empty filters
        A.CallTo(() => _mockSearchService.SearchAsync("", A<SearchFilters>.Ignored, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task SearchCommand_WithContentTypeFilter_ShouldPassFilters()
    {
        // Arrange
        var searchResults = new SearchResults
        {
            Clips = new List<Clip>(),
            TotalMatches = 0,
            Query = "test",
        };

        SearchFilters? capturedFilters = null;
        A.CallTo(() => _mockSearchService.SearchAsync(A<string>.Ignored, A<SearchFilters>.Ignored, A<CancellationToken>.Ignored))
            .Invokes((string _, SearchFilters? f, CancellationToken _) => capturedFilters = f)
            .Returns(searchResults);

        _viewModel.SearchText = "test";
        _viewModel.FilterByText = true;
        _viewModel.FilterByImage = false;

        // Act
        await _viewModel.SearchCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(capturedFilters).IsNotNull();
        await Assert.That(capturedFilters!.ContentTypes).IsNotNull();
        await Assert.That(capturedFilters.ContentTypes!.Contains(ClipType.Text)).IsTrue();
        await Assert.That(capturedFilters.ContentTypes!.Contains(ClipType.Image)).IsFalse();
    }

    [Test]
    public async Task SearchCommand_WithAllContentTypesChecked_PassesNoContentTypeFilter()
    {
        // Arrange - the dialog has no type checkboxes, so all three stay checked; that must not
        // exclude rich text, HTML or custom clips now that the search service filters on type
        SearchFilters? capturedFilters = null;
        A.CallTo(() => _mockSearchService.SearchAsync(A<string>.Ignored, A<SearchFilters>.Ignored, A<CancellationToken>.Ignored))
            .Invokes((string _, SearchFilters? f, CancellationToken _) => capturedFilters = f)
            .Returns(new SearchResults { Clips = [], TotalMatches = 0, Query = "" });

        _viewModel.FilterByText = true;
        _viewModel.FilterByImage = true;
        _viewModel.FilterByFiles = true;

        // Act
        await _viewModel.SearchCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(capturedFilters).IsNotNull();
        await Assert.That(capturedFilters!.ContentTypes).IsNull();
    }

    [Test]
    public async Task SearchCommand_WithDateRangeFilter_ShouldPassDateRange()
    {
        // Arrange
        var fromDate = DateTime.Today.AddDays(-7);
        var toDate = DateTime.Today;

        var searchResults = new SearchResults
        {
            Clips = new List<Clip>(),
            TotalMatches = 0,
            Query = "test",
        };

        SearchFilters? capturedFilters = null;
        A.CallTo(() => _mockSearchService.SearchAsync(A<string>.Ignored, A<SearchFilters>.Ignored, A<CancellationToken>.Ignored))
            .Invokes((string _, SearchFilters? f, CancellationToken _) => capturedFilters = f)
            .Returns(searchResults);

        _viewModel.SearchText = "test";
        _viewModel.CapturedAfterEnabled = true;
        _viewModel.CapturedBeforeEnabled = true;
        _viewModel.DateFrom = fromDate;
        _viewModel.DateTo = toDate;

        // Act
        await _viewModel.SearchCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(capturedFilters).IsNotNull();
        await Assert.That(capturedFilters!.DateRange).IsNotNull();
        await Assert.That(capturedFilters.DateRange!.From).IsEqualTo(fromDate);
        await Assert.That(capturedFilters.DateRange.To).IsEqualTo(toDate);
    }

    [Test]
    public async Task SearchCommand_WithCaseSensitiveEnabled_ShouldPassCaseSensitiveFlag()
    {
        // Arrange
        var searchResults = new SearchResults
        {
            Clips = new List<Clip>(),
            TotalMatches = 0,
            Query = "Test",
        };

        SearchFilters? capturedFilters = null;
        A.CallTo(() => _mockSearchService.SearchAsync(A<string>.Ignored, A<SearchFilters>.Ignored, A<CancellationToken>.Ignored))
            .Invokes((string _, SearchFilters? f, CancellationToken _) => capturedFilters = f)
            .Returns(searchResults);

        _viewModel.SearchText = "Test";
        _viewModel.IsCaseSensitive = true;

        // Act
        await _viewModel.SearchCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(capturedFilters).IsNotNull();
        await Assert.That(capturedFilters!.CaseSensitive).IsTrue();
    }

    [Test]
    public async Task SearchCommand_WithRegexEnabled_ShouldPassRegexFlag()
    {
        // Arrange
        var searchResults = new SearchResults
        {
            Clips = new List<Clip>(),
            TotalMatches = 0,
            Query = "\\d+",
        };

        SearchFilters? capturedFilters = null;
        A.CallTo(() => _mockSearchService.SearchAsync(A<string>.Ignored, A<SearchFilters>.Ignored, A<CancellationToken>.Ignored))
            .Invokes((string _, SearchFilters? f, CancellationToken _) => capturedFilters = f)
            .Returns(searchResults);

        _viewModel.SearchText = "\\d+";
        _viewModel.IsRegex = true;

        // Act
        await _viewModel.SearchCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(capturedFilters).IsNotNull();
        await Assert.That(capturedFilters!.IsRegex).IsTrue();
    }

    [Test]
    public async Task ClearSearchCommand_ShouldClearSearchTextAndResults()
    {
        // Arrange
        _viewModel.SearchText = "test";
        _viewModel.SearchResults.Add(new Clip { Id = Guid.NewGuid(), TextContent = "Result", Type = ClipType.Text, CapturedAt = DateTime.UtcNow });

        // Act
        await _viewModel.ClearSearchCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(_viewModel.SearchText).IsEqualTo(string.Empty);
        await Assert.That(_viewModel.SearchResults.Count).IsEqualTo(0);
    }

    [Test]
    public async Task LoadSearchHistoryCommand_ShouldLoadHistoryFromService()
    {
        // Arrange
        var queries = new List<SavedSearchQuery>
        {
            new() { Name = "Query 1", Query = "query 1" },
            new() { Name = "Query 2", Query = "query 2" },
            new() { Name = "Query 3", Query = "query 3" },
        };

        A.CallTo(() => _mockSearchService.GetSavedQueriesAsync(A<CancellationToken>.Ignored))
            .Returns(queries.AsReadOnly());

        // Act
        await _viewModel.LoadSavedQueriesCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(_viewModel.SavedQueries.Count).IsEqualTo(3);
        await Assert.That(_viewModel.SavedQueries[0].Name).IsEqualTo("Query 1");
    }

    [Test]
    public async Task IsSearching_WhenSearchCommandExecuting_ShouldBeTrue()
    {
        // Arrange
        var tcs = new TaskCompletionSource<SearchResults>();
        A.CallTo(() => _mockSearchService.SearchAsync(A<string>.Ignored, A<SearchFilters>.Ignored, A<CancellationToken>.Ignored))
            .Returns(tcs.Task);

        _viewModel.SearchText = "test";
        var propertyChangedCount = 0;
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.IsSearching))
                propertyChangedCount++;
        };

        // Act
        _ = _viewModel.SearchCommand.ExecuteAsync(null);
        // Assert - IsSearching should be true while executing
        await Assert.That(_viewModel.IsSearching).IsTrue();
        await Assert.That(propertyChangedCount).IsGreaterThan(0);

        // Complete the search
        tcs.SetResult(new SearchResults { Clips = new List<Clip>(), TotalMatches = 0, Query = "test" });
    }

    [Test]
    public async Task SavedQuerySelection_ShouldLoadFiltersFromJson()
    {
        // Arrange - Create a saved query with filters
        var filters = new SearchFilters
        {
            TitleQuery = "test title",
            TextContentQuery = "test content",
            CreatorQuery = "test creator",
            SourceUrlQuery = "http://test.com",
            Format = "text",
            DateRange = new DateRange(DateTime.Today.AddDays(-7), DateTime.Today),
            ContentTypes = [ClipType.Text, ClipType.Image],
            EncryptedOnly = true,
            HasShortcutOnly = true,
            IncludeDeleted = true,
            CaseSensitive = true,
            IsRegex = true,
            Scope = SearchScope.AllCollections,
        };

        var filtersJson = JsonSerializer.Serialize(filters);
        var savedQuery = new SavedSearchQuery
        {
            Name = "Test Query",
            Query = "test title",
            IsCaseSensitive = true,
            IsRegex = true,
            FiltersJson = filtersJson,
        };

        // Act - Select the saved query
        _viewModel.SelectedSavedQuery = savedQuery;

        // Assert - Verify all filters are loaded
        await Assert.That(_viewModel.SearchText).IsEqualTo("test title");
        await Assert.That(_viewModel.SearchTitleEnabled).IsTrue();
        await Assert.That(_viewModel.SearchClipText).IsEqualTo("test content");
        await Assert.That(_viewModel.SearchClipTextEnabled).IsTrue();
        await Assert.That(_viewModel.SearchCreator).IsEqualTo("test creator");
        await Assert.That(_viewModel.SearchCreatorEnabled).IsTrue();
        await Assert.That(_viewModel.SearchSourceUrl).IsEqualTo("http://test.com");
        await Assert.That(_viewModel.SearchSourceUrlEnabled).IsTrue();
        await Assert.That(_viewModel.SelectedFormat).IsEqualTo("text");
        await Assert.That(_viewModel.HasFormatEnabled).IsTrue();
        await Assert.That(_viewModel.DateFrom).IsEqualTo(DateTime.Today.AddDays(-7));
        await Assert.That(_viewModel.CapturedAfterEnabled).IsTrue();
        await Assert.That(_viewModel.DateTo).IsEqualTo(DateTime.Today);
        await Assert.That(_viewModel.CapturedBeforeEnabled).IsTrue();
        await Assert.That(_viewModel.FilterByText).IsTrue();
        await Assert.That(_viewModel.FilterByImage).IsTrue();
        await Assert.That(_viewModel.FilterByFiles).IsFalse();
        await Assert.That(_viewModel.EncryptedOnly).IsTrue();
        await Assert.That(_viewModel.HasShortcutOnly).IsTrue();
        await Assert.That(_viewModel.IncludeDeletedClips).IsTrue();
        await Assert.That(_viewModel.IsCaseSensitive).IsTrue();
        await Assert.That(_viewModel.IsRegex).IsTrue();
    }

    [Test]
    public async Task SavedQuerySelection_WithNullFiltersJson_ShouldLoadBasicProperties()
    {
        // Arrange - Create a saved query without filters JSON
        var savedQuery = new SavedSearchQuery
        {
            Name = "Simple Query",
            Query = "simple test",
            IsCaseSensitive = false,
            IsRegex = false,
            FiltersJson = null,
        };

        // Act - Select the saved query
        _viewModel.SelectedSavedQuery = savedQuery;

        // Assert - Verify basic properties are loaded
        await Assert.That(_viewModel.SearchText).IsEqualTo("simple test");
        await Assert.That(_viewModel.IsCaseSensitive).IsFalse();
        await Assert.That(_viewModel.IsRegex).IsFalse();
    }
}
