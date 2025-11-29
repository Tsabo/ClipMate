using System.Collections.ObjectModel;
using ClipMate.App.ViewModels;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Search;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Tests for SearchViewModel.
/// </summary>
public class SearchViewModelTests
{
    private readonly Mock<ISearchService> _mockSearchService;
    private readonly Mock<IMessenger> _mockMessenger;
    private readonly Mock<IServiceScopeFactory> _mockServiceScopeFactory;
    private readonly SearchViewModel _viewModel;

    public SearchViewModelTests()
    {
        _mockSearchService = new Mock<ISearchService>();
        _mockMessenger = new Mock<IMessenger>();
        
        // Create mock service scope factory
        var mockServiceScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        mockServiceProvider.Setup(x => x.GetService(typeof(ISearchService))).Returns(_mockSearchService.Object);
        mockServiceScope.Setup(x => x.ServiceProvider).Returns(mockServiceProvider.Object);
        
        _mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        _mockServiceScopeFactory.Setup(x => x.CreateScope()).Returns(mockServiceScope.Object);
        
        _viewModel = new SearchViewModel(_mockServiceScopeFactory.Object, _mockMessenger.Object);
    }

    [Test]
    public async Task Constructor_WithNullSearchService_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => new SearchViewModel(null!, _mockMessenger.Object))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task SearchText_WhenSet_ShouldRaisePropertyChanged()
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
        await Assert.That(propertyChangedRaised).IsTrue();
        await Assert.That(_viewModel.SearchText).IsEqualTo("test query");
    }

    [Test]
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
        await Assert.That(_viewModel.SearchResults.Count).IsEqualTo(1);
        await Assert.That(_viewModel.TotalMatches).IsEqualTo(1);
        _mockSearchService.Verify(s => s.SearchAsync("test", It.IsAny<SearchFilters>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task SearchCommand_WithEmptyQuery_ShouldClearResults()
    {
        // Arrange
        _viewModel.SearchText = "";

        // Act
        await _viewModel.SearchCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(_viewModel.SearchResults.Count).IsEqualTo(0);
        await Assert.That(_viewModel.TotalMatches).IsEqualTo(0);
        _mockSearchService.Verify(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<SearchFilters>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
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
        await Assert.That(capturedFilters).IsNotNull();
        await Assert.That(capturedFilters!.ContentTypes).IsNotNull();
        await Assert.That(capturedFilters.ContentTypes!.Contains(ClipType.Text)).IsTrue();
        await Assert.That(capturedFilters.ContentTypes.Contains(ClipType.Image)).IsFalse();
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
        var history = new List<string> { "query 1", "query 2", "query 3" };
        _mockSearchService
            .Setup(s => s.GetSearchHistoryAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(history.AsReadOnly());

        // Act
        await _viewModel.LoadSearchHistoryCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(_viewModel.SearchHistory.Count).IsEqualTo(3);
        await Assert.That(_viewModel.SearchHistory[0]).IsEqualTo("query 1");
    }

    [Test]
    public async Task IsSearching_WhenSearchCommandExecuting_ShouldBeTrue()
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
        await Assert.That(_viewModel.IsSearching).IsTrue();
        await Assert.That(propertyChangedCount).IsGreaterThan(0);

        // Complete the search
        tcs.SetResult(new SearchResults { Clips = new List<Clip>(), TotalMatches = 0, Query = "test" });
    }
}
