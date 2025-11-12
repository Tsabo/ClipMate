using System.Collections.ObjectModel;
using ClipMate.App.ViewModels;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using Moq;
using Shouldly;
using Xunit;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Tests for SearchViewModel.
/// </summary>
public class SearchViewModelTests
{
    private readonly Mock<ISearchService> _mockSearchService;
    private readonly SearchViewModel _viewModel;

    public SearchViewModelTests()
    {
        _mockSearchService = new Mock<ISearchService>();
        _viewModel = new SearchViewModel(_mockSearchService.Object);
    }

    [Fact]
    public void Constructor_WithNullSearchService_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new SearchViewModel(null!));
    }

    [Fact]
    public void SearchText_WhenSet_ShouldRaisePropertyChanged()
    {
        // Arrange
        var propertyChangedRaised = false;
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.SearchText))
            {
                propertyChangedRaised = true;
            }
        };

        // Act
        _viewModel.SearchText = "test query";

        // Assert
        propertyChangedRaised.ShouldBeTrue();
        _viewModel.SearchText.ShouldBe("test query");
    }

    [Fact]
    public async Task SearchCommand_WithValidQuery_ShouldReturnResults()
    {
        // Arrange
        var clips = new List<Clip>
        {
            new Clip { Id = Guid.NewGuid(), TextContent = "Test result", Type = ClipType.Text, CapturedAt = DateTime.UtcNow }
        };

        var searchResults = new SearchResults
        {
            Clips = clips,
            TotalMatches = 1,
            Query = "test"
        };

        _mockSearchService
            .Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<SearchFilters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        _viewModel.SearchText = "test";

        // Act
        await _viewModel.SearchCommand.ExecuteAsync(null);

        // Assert
        _viewModel.SearchResults.Count.ShouldBe(1);
        _viewModel.TotalMatches.ShouldBe(1);
        _mockSearchService.Verify(s => s.SearchAsync("test", It.IsAny<SearchFilters>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchCommand_WithEmptyQuery_ShouldClearResults()
    {
        // Arrange
        _viewModel.SearchText = "";

        // Act
        await _viewModel.SearchCommand.ExecuteAsync(null);

        // Assert
        _viewModel.SearchResults.Count.ShouldBe(0);
        _viewModel.TotalMatches.ShouldBe(0);
        _mockSearchService.Verify(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<SearchFilters>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchCommand_WithContentTypeFilter_ShouldPassFilters()
    {
        // Arrange
        var searchResults = new SearchResults
        {
            Clips = new List<Clip>(),
            TotalMatches = 0,
            Query = "test"
        };

        SearchFilters? capturedFilters = null;
        _mockSearchService
            .Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<SearchFilters>(), It.IsAny<CancellationToken>()))
            .Callback<string, SearchFilters?, CancellationToken>((q, f, ct) => capturedFilters = f)
            .ReturnsAsync(searchResults);

        _viewModel.SearchText = "test";
        _viewModel.FilterByText = true;
        _viewModel.FilterByImage = false;

        // Act
        await _viewModel.SearchCommand.ExecuteAsync(null);

        // Assert
        capturedFilters.ShouldNotBeNull();
        capturedFilters.ContentTypes.ShouldNotBeNull();
        capturedFilters.ContentTypes.ShouldContain(ClipType.Text);
        capturedFilters.ContentTypes.ShouldNotContain(ClipType.Image);
    }

    [Fact]
    public async Task SearchCommand_WithDateRangeFilter_ShouldPassDateRange()
    {
        // Arrange
        var fromDate = DateTime.Today.AddDays(-7);
        var toDate = DateTime.Today;

        var searchResults = new SearchResults
        {
            Clips = new List<Clip>(),
            TotalMatches = 0,
            Query = "test"
        };

        SearchFilters? capturedFilters = null;
        _mockSearchService
            .Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<SearchFilters>(), It.IsAny<CancellationToken>()))
            .Callback<string, SearchFilters?, CancellationToken>((q, f, ct) => capturedFilters = f)
            .ReturnsAsync(searchResults);

        _viewModel.SearchText = "test";
        _viewModel.DateFrom = fromDate;
        _viewModel.DateTo = toDate;

        // Act
        await _viewModel.SearchCommand.ExecuteAsync(null);

        // Assert
        capturedFilters.ShouldNotBeNull();
        capturedFilters.DateRange.ShouldNotBeNull();
        capturedFilters.DateRange.From.ShouldBe(fromDate);
        capturedFilters.DateRange.To.ShouldBe(toDate);
    }

    [Fact]
    public async Task SearchCommand_WithCaseSensitiveEnabled_ShouldPassCaseSensitiveFlag()
    {
        // Arrange
        var searchResults = new SearchResults
        {
            Clips = new List<Clip>(),
            TotalMatches = 0,
            Query = "Test"
        };

        SearchFilters? capturedFilters = null;
        _mockSearchService
            .Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<SearchFilters>(), It.IsAny<CancellationToken>()))
            .Callback<string, SearchFilters?, CancellationToken>((q, f, ct) => capturedFilters = f)
            .ReturnsAsync(searchResults);

        _viewModel.SearchText = "Test";
        _viewModel.IsCaseSensitive = true;

        // Act
        await _viewModel.SearchCommand.ExecuteAsync(null);

        // Assert
        capturedFilters.ShouldNotBeNull();
        capturedFilters.CaseSensitive.ShouldBeTrue();
    }

    [Fact]
    public async Task SearchCommand_WithRegexEnabled_ShouldPassRegexFlag()
    {
        // Arrange
        var searchResults = new SearchResults
        {
            Clips = new List<Clip>(),
            TotalMatches = 0,
            Query = "\\d+"
        };

        SearchFilters? capturedFilters = null;
        _mockSearchService
            .Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<SearchFilters>(), It.IsAny<CancellationToken>()))
            .Callback<string, SearchFilters?, CancellationToken>((q, f, ct) => capturedFilters = f)
            .ReturnsAsync(searchResults);

        _viewModel.SearchText = "\\d+";
        _viewModel.IsRegex = true;

        // Act
        await _viewModel.SearchCommand.ExecuteAsync(null);

        // Assert
        capturedFilters.ShouldNotBeNull();
        capturedFilters.IsRegex.ShouldBeTrue();
    }

    [Fact]
    public async Task ClearSearchCommand_ShouldClearSearchTextAndResults()
    {
        // Arrange
        _viewModel.SearchText = "test";
        _viewModel.SearchResults.Add(new Clip { Id = Guid.NewGuid(), TextContent = "Result", Type = ClipType.Text, CapturedAt = DateTime.UtcNow });

        // Act
        await _viewModel.ClearSearchCommand.ExecuteAsync(null);

        // Assert
        _viewModel.SearchText.ShouldBe(string.Empty);
        _viewModel.SearchResults.Count.ShouldBe(0);
    }

    [Fact]
    public async Task LoadSearchHistoryCommand_ShouldLoadHistoryFromService()
    {
        // Arrange
        var history = new List<string> { "query 1", "query 2", "query 3" };
        _mockSearchService
            .Setup(s => s.GetSearchHistoryAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(history.AsReadOnly());

        // Act
        await _viewModel.LoadSearchHistoryCommand.ExecuteAsync(null);

        // Assert
        _viewModel.SearchHistory.Count.ShouldBe(3);
        _viewModel.SearchHistory[0].ShouldBe("query 1");
    }

    [Fact]
    public void IsSearching_WhenSearchCommandExecuting_ShouldBeTrue()
    {
        // Arrange
        var tcs = new TaskCompletionSource<SearchResults>();
        _mockSearchService
            .Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<SearchFilters>(), It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);

        _viewModel.SearchText = "test";
        var propertyChangedCount = 0;
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.IsSearching))
            {
                propertyChangedCount++;
            }
        };

        // Act
        var task = _viewModel.SearchCommand.ExecuteAsync(null);

        // Assert - IsSearching should be true while executing
        _viewModel.IsSearching.ShouldBeTrue();
        propertyChangedCount.ShouldBeGreaterThan(0);

        // Complete the search
        tcs.SetResult(new SearchResults { Clips = new List<Clip>(), TotalMatches = 0, Query = "test" });
    }
}
