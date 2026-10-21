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
/// Integration tests for SearchService filters executed against a real SQLite database.
/// </summary>
public class SearchServiceFilterTests : IntegrationTestBase
{
    private SearchService CreateSearchService()
    {
        var clipRepository = new ClipRepository(DbContext, A.Fake<ILogger<ClipRepository>>());
        var contextFactory = A.Fake<IDatabaseContextFactory>();
        A.CallTo(() => contextFactory.GetClipRepository(A<string>.Ignored)).Returns(clipRepository);
        var collectionService = A.Fake<ICollectionService>();
        A.CallTo(() => collectionService.GetActiveDatabaseKey()).Returns("test-db");
        var configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => configurationService.Configuration).Returns(new ClipMateConfiguration());

        return new SearchService(contextFactory,
            collectionService,
            configurationService,
            A.Fake<ISqlValidationService>(),
            A.Fake<ILogger<SearchService>>());
    }

    private async Task<Clip> AddClipAsync(string title,
        Guid? collectionId = null,
        Guid? folderId = null,
        ClipType type = ClipType.Text,
        DateTimeOffset? capturedAt = null,
        string? creator = null,
        string? sourceUrl = null,
        string? text = null)
    {
        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            Title = title,
            Creator = creator,
            SourceUrl = sourceUrl,
            CollectionId = collectionId,
            FolderId = folderId,
            CapturedAt = capturedAt ?? DateTimeOffset.Now,
            ContentHash = Guid.NewGuid().ToString("N"),
            Type = type,
        };

        DbContext.Clips.Add(clip);

        if (text != null)
        {
            var clipData = new ClipData
            {
                Id = Guid.NewGuid(),
                ClipId = clip.Id,
                FormatName = "CF_UNICODETEXT",
                Format = 13,
                StorageType = 1,
            };

            DbContext.ClipData.Add(clipData);
            DbContext.BlobTxt.Add(new BlobTxt
            {
                Id = Guid.NewGuid(),
                ClipDataId = clipData.Id,
                ClipId = clip.Id,
                Data = text,
            });
        }

        await DbContext.SaveChangesAsync();
        return clip;
    }

    private static List<string> TitlesOf(SearchResults results) => results.Clips.Select(p => p.Title ?? string.Empty).OrderBy(p => p).ToList();

    [Test]
    public async Task SearchAsync_WithCurrentCollectionScope_ReturnsOnlyClipsInThatCollection()
    {
        // Arrange
        var inboxId = Guid.NewGuid();
        await AddClipAsync("in inbox", inboxId);
        await AddClipAsync("elsewhere", Guid.NewGuid());
        var searchService = CreateSearchService();

        // Act
        var results = await searchService.SearchAsync("", new SearchFilters
        {
            Scope = SearchScope.CurrentCollection,
            CollectionId = inboxId,
        });

        // Assert
        await Assert.That(TitlesOf(results)).IsEquivalentTo(["in inbox"]);
    }

    [Test]
    public async Task SearchAsync_WithCurrentCollectionScopeAndNoCollectionId_ThrowsArgumentException()
    {
        // Arrange
        var searchService = CreateSearchService();

        // Act & Assert
        await Assert.That(async () => await searchService.SearchAsync("", new SearchFilters { Scope = SearchScope.CurrentCollection }))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task SearchAsync_WithSpecificFolderScope_ReturnsOnlyClipsInThatFolder()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        await AddClipAsync("in folder", collectionId, folderId);
        await AddClipAsync("other folder", collectionId, Guid.NewGuid());
        await AddClipAsync("no folder", collectionId);
        var searchService = CreateSearchService();

        // Act
        var results = await searchService.SearchAsync("", new SearchFilters
        {
            Scope = SearchScope.SpecificFolder,
            FolderId = folderId,
        });

        // Assert
        await Assert.That(TitlesOf(results)).IsEquivalentTo(["in folder"]);
    }

    [Test]
    public async Task SearchAsync_WithSpecificFolderScopeAndNoFolderId_ThrowsArgumentException()
    {
        // Arrange
        var searchService = CreateSearchService();

        // Act & Assert
        await Assert.That(async () => await searchService.SearchAsync("", new SearchFilters { Scope = SearchScope.SpecificFolder }))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task SearchAsync_WithAllCollectionsScope_IgnoresCollectionId()
    {
        // Arrange
        await AddClipAsync("first", Guid.NewGuid());
        await AddClipAsync("second", Guid.NewGuid());
        var searchService = CreateSearchService();

        // Act
        var results = await searchService.SearchAsync("", new SearchFilters
        {
            Scope = SearchScope.AllCollections,
            CollectionId = Guid.NewGuid(),
        });

        // Assert
        await Assert.That(TitlesOf(results)).IsEquivalentTo(["first", "second"]);
    }

    [Test]
    public async Task SearchAsync_WithContentTypes_ReturnsOnlyClipsOfThoseTypes()
    {
        // Arrange
        await AddClipAsync("text", type: ClipType.Text);
        await AddClipAsync("image", type: ClipType.Image);
        await AddClipAsync("files", type: ClipType.Files);
        var searchService = CreateSearchService();

        // Act
        var results = await searchService.SearchAsync("", new SearchFilters { ContentTypes = [ClipType.Image, ClipType.Files] });

        // Assert
        await Assert.That(TitlesOf(results)).IsEquivalentTo(["files", "image"]);
    }

    [Test]
    public async Task SearchAsync_WithNullContentTypes_ReturnsClipsOfEveryType()
    {
        // Arrange
        await AddClipAsync("text", type: ClipType.Text);
        await AddClipAsync("html", type: ClipType.Html);
        await AddClipAsync("custom", type: ClipType.Custom);
        var searchService = CreateSearchService();

        // Act
        var results = await searchService.SearchAsync("", new SearchFilters { ContentTypes = null });

        // Assert
        await Assert.That(TitlesOf(results)).IsEquivalentTo(["custom", "html", "text"]);
    }

    [Test]
    public async Task SearchAsync_WithDateRange_ReturnsClipsCapturedFromAfterDayUntilBeforeDay()
    {
        // Arrange
        var offset = TimeSpan.Zero;
        await AddClipAsync("too old", capturedAt: new DateTimeOffset(2026, 3, 9, 23, 59, 0, offset));
        await AddClipAsync("first day", capturedAt: new DateTimeOffset(2026, 3, 10, 0, 1, 0, offset));
        await AddClipAsync("last day before", capturedAt: new DateTimeOffset(2026, 3, 19, 23, 0, 0, offset));
        await AddClipAsync("too new", capturedAt: new DateTimeOffset(2026, 3, 20, 8, 0, 0, offset));
        var searchService = CreateSearchService();

        // Act
        var results = await searchService.SearchAsync("", new SearchFilters
        {
            DateRange = new DateRange(new DateTime(2026, 3, 10), new DateTime(2026, 3, 20)),
        });

        // Assert
        await Assert.That(TitlesOf(results)).IsEquivalentTo(["first day", "last day before"]);
    }

    [Test]
    public async Task SearchAsync_WithQueryText_ReturnsClipsWhoseTextMatches()
    {
        // Arrange
        await AddClipAsync("match", text: "Hello World");
        await AddClipAsync("no match", text: "Goodbye");
        var searchService = CreateSearchService();

        // Act
        var results = await searchService.SearchAsync("world");

        // Assert
        await Assert.That(TitlesOf(results)).IsEquivalentTo(["match"]);
    }

    [Test]
    public async Task SearchAsync_WithTextContentCreatorAndSourceUrl_ReturnsOnlyClipsMatchingAll()
    {
        // Arrange
        await AddClipAsync("match", creator: "Alice", sourceUrl: "https://example.com/a", text: "alpha beta");
        await AddClipAsync("wrong creator", creator: "Bob", sourceUrl: "https://example.com/a", text: "alpha beta");
        await AddClipAsync("wrong url", creator: "Alice", sourceUrl: "https://other.org", text: "alpha beta");
        await AddClipAsync("wrong text", creator: "Alice", sourceUrl: "https://example.com/a", text: "gamma");
        var searchService = CreateSearchService();

        // Act
        var results = await searchService.SearchAsync("", new SearchFilters
        {
            TextContentQuery = "beta",
            CreatorQuery = "alice",
            SourceUrlQuery = "example.com",
        });

        // Assert
        await Assert.That(TitlesOf(results)).IsEquivalentTo(["match"]);
    }

    [Test]
    public async Task SearchAsync_WithFormat_ReturnsOnlyClipsHavingThatFormat()
    {
        // Arrange
        var withText = await AddClipAsync("has text");
        await AddClipAsync("no formats");
        DbContext.ClipData.Add(new ClipData
        {
            Id = Guid.NewGuid(),
            ClipId = withText.Id,
            FormatName = "CF_TEXT",
            Format = 1,
            StorageType = 1,
        });

        await DbContext.SaveChangesAsync();
        var searchService = CreateSearchService();

        // Act
        var results = await searchService.SearchAsync("", new SearchFilters { Format = "text" });

        // Assert
        await Assert.That(TitlesOf(results)).IsEquivalentTo(["has text"]);
    }

    [Test]
    [Arguments("O'Brien's \"notes\"")]
    [Arguments("100% done")]
    [Arguments("snake_case")]
    [Arguments("a; DROP TABLE Clips; --")]
    [Arguments("' OR 1=1 --")]
    [Arguments("SELECT * FROM Clips WHERE 1=1 UNION SELECT")]
    [Arguments("@TitleQuery")]
    public async Task SearchAsync_WithHostileTitleValue_MatchesOnlyThatTitleAndLeavesDatabaseIntact(string hostile)
    {
        // Arrange
        await AddClipAsync(hostile);
        await AddClipAsync("unrelated");
        var searchService = CreateSearchService();

        // Act
        var results = await searchService.SearchAsync("", new SearchFilters { TitleQuery = hostile });

        // Assert
        await Assert.That(TitlesOf(results)).IsEquivalentTo([hostile]);
        await Assert.That(DbContext.Clips.Count()).IsEqualTo(2);
    }

    [Test]
    public async Task SearchAsync_WithUnderscoreInValue_DoesNotTreatItAsWildcard()
    {
        // Arrange
        await AddClipAsync("snake_case");
        await AddClipAsync("snakeXcase");
        var searchService = CreateSearchService();

        // Act
        var results = await searchService.SearchAsync("", new SearchFilters { TitleQuery = "snake_case" });

        // Assert
        await Assert.That(TitlesOf(results)).IsEquivalentTo(["snake_case"]);
    }

    [Test]
    public async Task BuildParameterizedSqlQuery_WithEveryValueFilter_KeepsValuesOutOfSqlText()
    {
        // Arrange
        var searchService = CreateSearchService();
        var collectionId = Guid.NewGuid();
        var filters = new SearchFilters
        {
            TitleQuery = "title'value",
            TextContentQuery = "content'value",
            CreatorQuery = "creator'value",
            SourceUrlQuery = "url'value",
            DateRange = new DateRange(new DateTime(2026, 1, 2), new DateTime(2026, 3, 4)),
            Scope = SearchScope.CurrentCollection,
            CollectionId = collectionId,
            ContentTypes = [ClipType.Image],
            Format = "html",
        };

        // Act
        var query = searchService.BuildParameterizedSqlQuery("query'value", filters);

        // Assert
        await Assert.That(query.Sql).DoesNotContain("'");
        await Assert.That(query.Sql).DoesNotContain("2026");
        await Assert.That(query.Sql).DoesNotContain(collectionId.ToString("D").ToUpperInvariant());
        await Assert.That(query.Parameters["TitleQuery"]).IsEqualTo("title'value");
        await Assert.That(query.Parameters["TextContentQuery"]).IsEqualTo("content'value");
        await Assert.That(query.Parameters["CreatorQuery"]).IsEqualTo("creator'value");
        await Assert.That(query.Parameters["SourceUrlQuery"]).IsEqualTo("url'value");
        await Assert.That(query.Parameters["Query"]).IsEqualTo("query'value");
        await Assert.That(query.Parameters["CapturedAfter"]).IsEqualTo("2026-01-02");
        await Assert.That(query.Parameters["CapturedBefore"]).IsEqualTo("2026-03-04");
        await Assert.That(query.Parameters["CollectionId"]).IsEqualTo(collectionId.ToString("D").ToUpperInvariant());
        await Assert.That(query.Parameters["ContentType0"]).IsEqualTo((int)ClipType.Image);
        await Assert.That(query.Parameters["Format"]).IsEqualTo(3);
    }

    [Test]
    public async Task BuildSqlQuery_WithQuotedValue_RendersExecutableSqlReturningSameClips()
    {
        // Arrange
        await AddClipAsync("O'Brien @ home");
        await AddClipAsync("unrelated");
        var searchService = CreateSearchService();
        var filters = new SearchFilters { TitleQuery = "O'Brien @ home" };

        // Act
        var readableSql = searchService.BuildSqlQuery("", filters);
        var clipRepository = new ClipRepository(DbContext, A.Fake<ILogger<ClipRepository>>());
        var clips = await clipRepository.ExecuteSqlQueryAsync(readableSql);

        // Assert
        await Assert.That(readableSql).Contains("TextSearch(Clips.TITLE, 'O''Brien @ home') = 1");
        await Assert.That(clips.Select(p => p.Title ?? string.Empty).ToList()).IsEquivalentTo(["O'Brien @ home"]);
    }
}
