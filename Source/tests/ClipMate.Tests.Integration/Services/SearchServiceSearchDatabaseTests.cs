using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Search;
using ClipMate.Core.Services;
using ClipMate.Data;
using ClipMate.Data.Repositories;
using ClipMate.Data.Services;
using FakeItEasy;
using Microsoft.Extensions.Logging;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// <see cref="SearchService.SearchDatabaseAsync" /> against a real SQLite database: the search a Peer runs for Remote Search.
/// </summary>
public class SearchServiceSearchDatabaseTests : IntegrationTestBase
{
    private IDatabaseContextFactory _contextFactory = null!;

    private SearchService CreateSearchService()
    {
        var clipRepository = new ClipRepository(DbContext, A.Fake<ILogger<ClipRepository>>());
        _contextFactory = A.Fake<IDatabaseContextFactory>();
        A.CallTo(() => _contextFactory.GetClipRepository("db_work")).Returns(clipRepository);
        var configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => configurationService.Configuration).Returns(new ClipMateConfiguration());

        return new SearchService(_contextFactory, A.Fake<ICollectionService>(), configurationService, A.Fake<ISqlValidationService>(),
            A.Fake<ILogger<SearchService>>());
    }

    private async Task AddClipAsync(string title, int sortKey, bool deleted = false)
    {
        DbContext.Clips.Add(new Clip
        {
            Id = Guid.NewGuid(),
            Title = title,
            SortKey = sortKey,
            Del = deleted,
            CapturedAt = DateTimeOffset.Now,
            ContentHash = Guid.NewGuid().ToString("N"),
            Type = ClipType.Text,
        });

        await DbContext.SaveChangesAsync();
    }

    [Test]
    public async Task SearchDatabaseAsync_MoreMatchesThanTheLimit_ReturnsTheNewestUpToTheLimitAndTheTotal()
    {
        // Arrange
        for (var i = 1; i <= 5; i++)
            await AddClipAsync($"report {i}", i);

        await AddClipAsync("holiday photo", 10);
        var searchService = CreateSearchService();

        // Act
        var matches = await searchService.SearchDatabaseAsync("db_work", new SearchFilters { TitleQuery = "report" }, 3);

        // Assert
        await Assert.That(matches.TotalMatches).IsEqualTo(5);
        await Assert.That(string.Join(", ", matches.Clips.Select(p => p.Title))).IsEqualTo("report 5, report 4, report 3");
    }

    [Test]
    public async Task SearchDatabaseAsync_WithoutALimit_ReturnsEveryMatchFromThatDatabase()
    {
        // Arrange
        await AddClipAsync("report a", 1);
        await AddClipAsync("report b", 2);
        await AddClipAsync("report deleted", 3, true);
        var searchService = CreateSearchService();

        // Act
        var matches = await searchService.SearchDatabaseAsync("db_work", new SearchFilters { TitleQuery = "report" });

        // Assert
        await Assert.That(matches.TotalMatches).IsEqualTo(2);
        await Assert.That(matches.Clips.Count).IsEqualTo(2);
        A.CallTo(() => _contextFactory.GetClipRepository("db_work")).MustHaveHappened();
    }

    [Test]
    public async Task SearchDatabaseAsync_AlreadyCancelled_Throws()
    {
        // Arrange
        await AddClipAsync("report", 1);
        var searchService = CreateSearchService();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        // Act / Assert
        await Assert.That(async () => await searchService.SearchDatabaseAsync("db_work", new SearchFilters { TitleQuery = "report" }, 10, cancelled.Token))
            .Throws<OperationCanceledException>();
    }
}
