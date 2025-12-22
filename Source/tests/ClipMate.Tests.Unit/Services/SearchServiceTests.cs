using ClipMate.Core.Models;
using ClipMate.Core.Models.Search;
using ClipMate.Core.Repositories;
using ClipMate.Data;
using ClipMate.Data.Services;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for SearchService.
/// </summary>
public class SearchServiceTests
{
    private readonly Mock<IClipRepository> _mockClipRepository;
    private readonly Mock<ClipMateDbContext> _mockDbContext;
    private readonly Mock<ISearchQueryRepository> _mockSearchQueryRepository;
    private readonly SearchService _searchService;

    public SearchServiceTests()
    {
        _mockClipRepository = new Mock<IClipRepository>();
        _mockSearchQueryRepository = new Mock<ISearchQueryRepository>();
        _mockDbContext = new Mock<ClipMateDbContext>();
        _searchService = new SearchService(_mockClipRepository.Object, _mockSearchQueryRepository.Object, _mockDbContext.Object);
    }

    [Test]
    public async Task Constructor_WithNullClipRepository_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() =>
                new SearchService(null!, _mockSearchQueryRepository.Object, _mockDbContext.Object))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithNullSearchQueryRepository_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() =>
                new SearchService(_mockClipRepository.Object, null!, _mockDbContext.Object))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task SearchAsync_WithEmptyQuery_ShouldReturnAllClips()
    {
        // Arrange
        var clips = new List<Clip>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TextContent = "Test 1",
                Type = ClipType.Text,
                CapturedAt = DateTime.UtcNow,
            },
            new()
            {
                Id = Guid.NewGuid(),
                TextContent = "Test 2",
                Type = ClipType.Text,
                CapturedAt = DateTime.UtcNow,
            },
        };

        _mockClipRepository
            .Setup(p => p.GetRecentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(clips);

        // Act
        var results = await _searchService.SearchAsync(string.Empty);

        // Assert
        await Assert.That(results.Clips.Count).IsEqualTo(2);
        await Assert.That(results.TotalMatches).IsEqualTo(2);
        await Assert.That(results.Query).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task SearchAsync_WithTextQuery_ShouldReturnMatchingClips()
    {
        // Arrange
        var matchingClip = new Clip
        {
            Id = Guid.NewGuid(),
            TextContent = "Hello World",
            Type = ClipType.Text,
            CapturedAt = DateTime.UtcNow,
        };

        var nonMatchingClip = new Clip
        {
            Id = Guid.NewGuid(),
            TextContent = "Goodbye",
            Type = ClipType.Text,
            CapturedAt = DateTime.UtcNow,
        };

        _mockClipRepository
            .Setup(p => p.GetRecentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Clip> { matchingClip, nonMatchingClip });

        // Act
        var results = await _searchService.SearchAsync("World");

        // Assert
        await Assert.That(results.Clips.Count).IsEqualTo(1);
        await Assert.That(results.Clips[0].TextContent).IsEqualTo("Hello World");
        await Assert.That(results.TotalMatches).IsEqualTo(1);
    }

    [Test]
    public async Task SearchAsync_WithCaseSensitiveFilter_ShouldRespectCase()
    {
        // Arrange
        var clips = new List<Clip>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TextContent = "Hello World",
                Type = ClipType.Text,
                CapturedAt = DateTime.UtcNow,
            },
            new()
            {
                Id = Guid.NewGuid(),
                TextContent = "hello world",
                Type = ClipType.Text,
                CapturedAt = DateTime.UtcNow,
            },
        };

        _mockClipRepository
            .Setup(p => p.GetRecentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(clips);

        var filters = new SearchFilters { CaseSensitive = true };

        // Act
        var results = await _searchService.SearchAsync("Hello", filters);

        // Assert
        await Assert.That(results.Clips.Count).IsEqualTo(1);
        await Assert.That(results.Clips[0].TextContent).IsEqualTo("Hello World");
    }

    [Test]
    public async Task SearchAsync_WithContentTypeFilter_ShouldFilterByType()
    {
        // Arrange
        var textClip = new Clip
        {
            Id = Guid.NewGuid(),
            TextContent = "Text content",
            Type = ClipType.Text,
            CapturedAt = DateTime.UtcNow,
        };

        var imageClip = new Clip
        {
            Id = Guid.NewGuid(),
            TextContent = "image.png",
            Type = ClipType.Image,
            CapturedAt = DateTime.UtcNow,
        };

        _mockClipRepository
            .Setup(p => p.GetRecentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Clip> { textClip, imageClip });

        var filters = new SearchFilters { ContentTypes = [ClipType.Text] };

        // Act
        var results = await _searchService.SearchAsync("", filters);

        // Assert
        await Assert.That(results.Clips.Count).IsEqualTo(1);
        await Assert.That(results.Clips[0].Type).IsEqualTo(ClipType.Text);
    }

    [Test]
    public async Task SearchAsync_WithDateRangeFilter_ShouldFilterByDate()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var oldClip = new Clip
        {
            Id = Guid.NewGuid(),
            TextContent = "Old",
            Type = ClipType.Text,
            CapturedAt = now.AddDays(-10),
        };

        var recentClip = new Clip
        {
            Id = Guid.NewGuid(),
            TextContent = "Recent",
            Type = ClipType.Text,
            CapturedAt = now.AddDays(-2),
        };

        _mockClipRepository
            .Setup(p => p.GetRecentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Clip> { oldClip, recentClip });

        var filters = new SearchFilters
        {
            DateRange = new DateRange(now.AddDays(-7), now),
        };

        // Act
        var results = await _searchService.SearchAsync("", filters);

        // Assert
        await Assert.That(results.Clips.Count).IsEqualTo(1);
        await Assert.That(results.Clips[0].TextContent).IsEqualTo("Recent");
    }

    [Test]
    public async Task SearchAsync_WithRegexFilter_ShouldMatchPattern()
    {
        // Arrange
        var emailClip = new Clip
        {
            Id = Guid.NewGuid(),
            TextContent = "Contact: john@example.com",
            Type = ClipType.Text,
            CapturedAt = DateTime.UtcNow,
        };

        var nonEmailClip = new Clip
        {
            Id = Guid.NewGuid(),
            TextContent = "No email here",
            Type = ClipType.Text,
            CapturedAt = DateTime.UtcNow,
        };

        _mockClipRepository
            .Setup(p => p.GetRecentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Clip> { emailClip, nonEmailClip });

        var filters = new SearchFilters { IsRegex = true };

        // Act - Email regex pattern
        var results = await _searchService.SearchAsync(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", filters);

        // Assert
        await Assert.That(results.Clips.Count).IsEqualTo(1);
        await Assert.That(results.Clips[0].TextContent).Contains("@example.com");
    }

    [Test]
    public async Task SearchAsync_WithInvalidRegex_ShouldThrowArgumentException()
    {
        // Arrange
        var filters = new SearchFilters { IsRegex = true };

        // Act & Assert
        await Assert.That(async () =>
                await _searchService.SearchAsync("[Invalid(Regex", filters))
            .Throws<ArgumentException>();
    }

    [Test]
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
            CreatedAt = DateTime.UtcNow,
        };

        _mockSearchQueryRepository
            .Setup(p => p.CreateAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedQuery);

        // Act
        var result = await _searchService.SaveSearchQueryAsync("My Search", "test", true, false);

        // Assert
        await Assert.That(result.Name).IsEqualTo("My Search");
        await Assert.That(result.QueryText).IsEqualTo("test");
        await Assert.That(result.IsCaseSensitive).IsTrue();
        await Assert.That(result.IsRegex).IsFalse();
        _mockSearchQueryRepository.Verify(r => r.CreateAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
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
            CreatedAt = DateTime.UtcNow,
        };

        var clips = new List<Clip>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TextContent = "Important document",
                Type = ClipType.Text,
                CapturedAt = DateTime.UtcNow,
            },
        };

        _mockSearchQueryRepository
            .Setup(p => p.GetByIdAsync(searchQueryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedQuery);

        _mockClipRepository
            .Setup(p => p.GetRecentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(clips);

        // Act
        var results = await _searchService.ExecuteSavedSearchAsync(searchQueryId);

        // Assert
        await Assert.That(results.Clips.Count).IsEqualTo(1);
        await Assert.That(results.Query).IsEqualTo("important");
    }

    [Test]
    public async Task DeleteSearchQueryAsync_ShouldDeleteQuery()
    {
        // Arrange
        var queryId = Guid.NewGuid();
        _mockSearchQueryRepository
            .Setup(p => p.DeleteAsync(queryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _searchService.DeleteSearchQueryAsync(queryId);

        // Assert
        _mockSearchQueryRepository.Verify(p => p.DeleteAsync(queryId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetSearchHistoryAsync_ShouldReturnRecentSearches()
    {
        // Arrange - This would typically be stored in a settings/history service
        // For now, we'll test that it returns an empty list until implementation

        // Act
        var history = await _searchService.GetSearchHistoryAsync();

        // Assert
        await Assert.That(history).IsNotNull();
        await Assert.That(history.Count).IsEqualTo(0); // Empty until we implement history tracking
    }
}
