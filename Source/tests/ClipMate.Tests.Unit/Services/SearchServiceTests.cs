using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Search;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using ClipMate.Data;
using ClipMate.Data.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for SearchService.
/// </summary>
public class SearchServiceTests
{
    private readonly Mock<IClipRepository> _mockClipRepository;
    private readonly Mock<IConfigurationService> _mockConfigurationService;
    private ClipMateDbContext _dbContext = null!;
    private SearchService _searchService = null!;

    public SearchServiceTests()
    {
        _mockClipRepository = new Mock<IClipRepository>();
        _mockConfigurationService = new Mock<IConfigurationService>();
    }

    [Before(Test)]
    public void Setup()
    {
        // Use SQLite in-memory database for testing (supports relational features and SQL execution)
        var options = new DbContextOptionsBuilder<ClipMateDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _dbContext = new ClipMateDbContext(options);
        _dbContext.Database.OpenConnection();
        _dbContext.Database.EnsureCreated();

        var config = new ClipMateConfiguration();
        _mockConfigurationService.Setup(c => c.Configuration).Returns(config);

        _searchService = new SearchService(_mockClipRepository.Object, _mockConfigurationService.Object, _dbContext);
    }

    [After(Test)]
    public void Cleanup()
    {
        _dbContext.Database.CloseConnection();
        _dbContext.Dispose();
    }

    [Test]
    public async Task Constructor_WithNullConfigurationService_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() =>
                new SearchService(_mockClipRepository.Object, null!, _dbContext))
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
            .Setup(p => p.ExecuteSqlQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
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

        _mockClipRepository
            .Setup(p => p.ExecuteSqlQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Clip> { matchingClip });

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

        var filters = new SearchFilters { CaseSensitive = true };

        _mockClipRepository
            .Setup(p => p.ExecuteSqlQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([clips[0]]);

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

        var filters = new SearchFilters { ContentTypes = [ClipType.Text] };

        _mockClipRepository
            .Setup(p => p.ExecuteSqlQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([textClip]);

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

        var recentClip = new Clip
        {
            Id = Guid.NewGuid(),
            TextContent = "Recent",
            Type = ClipType.Text,
            CapturedAt = now.AddDays(-2),
        };

        var filters = new SearchFilters
        {
            DateRange = new DateRange(now.AddDays(-7), now),
        };

        _mockClipRepository
            .Setup(p => p.ExecuteSqlQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([recentClip]);

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

        var filters = new SearchFilters { IsRegex = true };

        _mockClipRepository
            .Setup(p => p.ExecuteSqlQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([emailClip]);

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
    public async Task SaveSearchQueryAsync_ShouldSaveQueryToConfiguration()
    {
        // Arrange
        var config = new ClipMateConfiguration();
        _mockConfigurationService.Setup(c => c.Configuration).Returns(config);
        _mockConfigurationService.Setup(c => c.SaveAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _searchService.SaveSearchQueryAsync("My Search", "test", true, false);

        // Assert
        await Assert.That(config.SavedSearchQueries.Count).IsEqualTo(1);
        await Assert.That(config.SavedSearchQueries[0].Name).IsEqualTo("My Search");
        await Assert.That(config.SavedSearchQueries[0].Query).IsEqualTo("test");
        await Assert.That(config.SavedSearchQueries[0].IsCaseSensitive).IsEqualTo(true);
        await Assert.That(config.SavedSearchQueries[0].IsRegex).IsEqualTo(false);
        _mockConfigurationService.Verify(c => c.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task DeleteSearchQueryAsync_ShouldDeleteQuery()
    {
        // Arrange
        var config = new ClipMateConfiguration();
        config.SavedSearchQueries.Add(new SavedSearchQuery { Name = "Test Query", Query = "test" });
        _mockConfigurationService.Setup(c => c.Configuration).Returns(config);
        _mockConfigurationService.Setup(c => c.SaveAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _searchService.DeleteSearchQueryAsync("Test Query");

        // Assert
        await Assert.That(config.SavedSearchQueries.Count).IsEqualTo(0);
        _mockConfigurationService.Verify(c => c.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
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
