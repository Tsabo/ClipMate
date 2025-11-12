using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using ClipMate.Data.Services;
using Moq;
using Shouldly;
using Xunit;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for SearchService.
/// </summary>
public class SearchServiceTests
{
    private readonly Mock<IClipRepository> _mockClipRepository;
    private readonly Mock<ISearchQueryRepository> _mockSearchQueryRepository;
    private readonly SearchService _searchService;

    public SearchServiceTests()
    {
        _mockClipRepository = new Mock<IClipRepository>();
        _mockSearchQueryRepository = new Mock<ISearchQueryRepository>();
        _searchService = new SearchService(_mockClipRepository.Object, _mockSearchQueryRepository.Object);
    }

    [Fact]
    public void Constructor_WithNullClipRepository_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new SearchService(null!, _mockSearchQueryRepository.Object));
    }

    [Fact]
    public void Constructor_WithNullSearchQueryRepository_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new SearchService(_mockClipRepository.Object, null!));
    }

    [Fact]
    public async Task SearchAsync_WithEmptyQuery_ShouldReturnAllClips()
    {
        // Arrange
        var clips = new List<Clip>
        {
            new Clip { Id = Guid.NewGuid(), TextContent = "Test 1", Type = ClipType.Text, CapturedAt = DateTime.UtcNow },
            new Clip { Id = Guid.NewGuid(), TextContent = "Test 2", Type = ClipType.Text, CapturedAt = DateTime.UtcNow }
        };

        _mockClipRepository
            .Setup(r => r.GetRecentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(clips);

        // Act
        var results = await _searchService.SearchAsync(string.Empty);

        // Assert
        results.Clips.Count.ShouldBe(2);
        results.TotalMatches.ShouldBe(2);
        results.Query.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task SearchAsync_WithTextQuery_ShouldReturnMatchingClips()
    {
        // Arrange
        var matchingClip = new Clip { Id = Guid.NewGuid(), TextContent = "Hello World", Type = ClipType.Text, CapturedAt = DateTime.UtcNow };
        var nonMatchingClip = new Clip { Id = Guid.NewGuid(), TextContent = "Goodbye", Type = ClipType.Text, CapturedAt = DateTime.UtcNow };
        
        _mockClipRepository
            .Setup(r => r.GetRecentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Clip> { matchingClip, nonMatchingClip });

        // Act
        var results = await _searchService.SearchAsync("World");

        // Assert
        results.Clips.Count.ShouldBe(1);
        results.Clips[0].TextContent.ShouldBe("Hello World");
        results.TotalMatches.ShouldBe(1);
    }

    [Fact]
    public async Task SearchAsync_WithCaseSensitiveFilter_ShouldRespectCase()
    {
        // Arrange
        var clips = new List<Clip>
        {
            new Clip { Id = Guid.NewGuid(), TextContent = "Hello World", Type = ClipType.Text, CapturedAt = DateTime.UtcNow },
            new Clip { Id = Guid.NewGuid(), TextContent = "hello world", Type = ClipType.Text, CapturedAt = DateTime.UtcNow }
        };

        _mockClipRepository
            .Setup(r => r.GetRecentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(clips);

        var filters = new SearchFilters { CaseSensitive = true };

        // Act
        var results = await _searchService.SearchAsync("Hello", filters);

        // Assert
        results.Clips.Count.ShouldBe(1);
        results.Clips[0].TextContent.ShouldBe("Hello World");
    }

    [Fact]
    public async Task SearchAsync_WithContentTypeFilter_ShouldFilterByType()
    {
        // Arrange
        var textClip = new Clip { Id = Guid.NewGuid(), TextContent = "Text content", Type = ClipType.Text, CapturedAt = DateTime.UtcNow };
        var imageClip = new Clip { Id = Guid.NewGuid(), TextContent = "image.png", Type = ClipType.Image, CapturedAt = DateTime.UtcNow };

        _mockClipRepository
            .Setup(r => r.GetRecentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Clip> { textClip, imageClip });

        var filters = new SearchFilters { ContentTypes = new[] { ClipType.Text } };

        // Act
        var results = await _searchService.SearchAsync("", filters);

        // Assert
        results.Clips.Count.ShouldBe(1);
        results.Clips[0].Type.ShouldBe(ClipType.Text);
    }

    [Fact]
    public async Task SearchAsync_WithDateRangeFilter_ShouldFilterByDate()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var oldClip = new Clip { Id = Guid.NewGuid(), TextContent = "Old", Type = ClipType.Text, CapturedAt = now.AddDays(-10) };
        var recentClip = new Clip { Id = Guid.NewGuid(), TextContent = "Recent", Type = ClipType.Text, CapturedAt = now.AddDays(-2) };

        _mockClipRepository
            .Setup(r => r.GetRecentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Clip> { oldClip, recentClip });

        var filters = new SearchFilters 
        { 
            DateRange = new DateRange(now.AddDays(-7), now)
        };

        // Act
        var results = await _searchService.SearchAsync("", filters);

        // Assert
        results.Clips.Count.ShouldBe(1);
        results.Clips[0].TextContent.ShouldBe("Recent");
    }

    [Fact]
    public async Task SearchAsync_WithRegexFilter_ShouldMatchPattern()
    {
        // Arrange
        var emailClip = new Clip { Id = Guid.NewGuid(), TextContent = "Contact: john@example.com", Type = ClipType.Text, CapturedAt = DateTime.UtcNow };
        var nonEmailClip = new Clip { Id = Guid.NewGuid(), TextContent = "No email here", Type = ClipType.Text, CapturedAt = DateTime.UtcNow };

        _mockClipRepository
            .Setup(r => r.GetRecentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Clip> { emailClip, nonEmailClip });

        var filters = new SearchFilters { IsRegex = true };

        // Act - Email regex pattern
        var results = await _searchService.SearchAsync(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", filters);

        // Assert
        results.Clips.Count.ShouldBe(1);
        results.Clips[0].TextContent.ShouldContain("@example.com");
    }

    [Fact]
    public async Task SearchAsync_WithInvalidRegex_ShouldThrowArgumentException()
    {
        // Arrange
        var filters = new SearchFilters { IsRegex = true };

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(async () =>
            await _searchService.SearchAsync("[Invalid(Regex", filters));
    }

    [Fact]
    public async Task SaveSearchQueryAsync_ShouldCreateAndReturnSearchQuery()
    {
        // Arrange
        var expectedQuery = new SearchQuery
        {
            Id = Guid.NewGuid(),
            Name = "My Search",
            QueryText = "test",
            IsCaseSensitive = true,
            IsRegex = false,
            CreatedAt = DateTime.UtcNow
        };

        _mockSearchQueryRepository
            .Setup(r => r.CreateAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedQuery);

        // Act
        var result = await _searchService.SaveSearchQueryAsync("My Search", "test", true, false);

        // Assert
        result.Name.ShouldBe("My Search");
        result.QueryText.ShouldBe("test");
        result.IsCaseSensitive.ShouldBeTrue();
        result.IsRegex.ShouldBeFalse();
        _mockSearchQueryRepository.Verify(r => r.CreateAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteSavedSearchAsync_ShouldExecuteQueryAndReturnResults()
    {
        // Arrange
        var searchQueryId = Guid.NewGuid();
        var savedQuery = new SearchQuery
        {
            Id = searchQueryId,
            Name = "Saved Search",
            QueryText = "important",
            IsCaseSensitive = false,
            IsRegex = false,
            CreatedAt = DateTime.UtcNow
        };

        var clips = new List<Clip>
        {
            new Clip { Id = Guid.NewGuid(), TextContent = "Important document", Type = ClipType.Text, CapturedAt = DateTime.UtcNow }
        };

        _mockSearchQueryRepository
            .Setup(r => r.GetByIdAsync(searchQueryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedQuery);

        _mockClipRepository
            .Setup(r => r.GetRecentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(clips);

        // Act
        var results = await _searchService.ExecuteSavedSearchAsync(searchQueryId);

        // Assert
        results.Clips.Count.ShouldBe(1);
        results.Query.ShouldBe("important");
    }

    [Fact]
    public async Task DeleteSearchQueryAsync_ShouldDeleteQuery()
    {
        // Arrange
        var queryId = Guid.NewGuid();
        _mockSearchQueryRepository
            .Setup(r => r.DeleteAsync(queryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _searchService.DeleteSearchQueryAsync(queryId);

        // Assert
        _mockSearchQueryRepository.Verify(r => r.DeleteAsync(queryId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetSearchHistoryAsync_ShouldReturnRecentSearches()
    {
        // Arrange - This would typically be stored in a settings/history service
        // For now, we'll test that it returns an empty list until implementation
        
        // Act
        var history = await _searchService.GetSearchHistoryAsync(10);

        // Assert
        history.ShouldNotBeNull();
        history.Count.ShouldBe(0); // Empty until we implement history tracking
    }
}
