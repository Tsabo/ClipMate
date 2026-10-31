using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Search;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data;
using ClipMate.Data.Repositories;
using ClipMate.Data.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FakeItEasy;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Integration tests for SearchService with real SQLite database.
/// </summary>
public class SearchServiceTests : IntegrationTestBase
{
    [Test]
    public async Task VerifyDatabaseSchema_TablesExist()
    {
        // Get list of tables created by EF Core
        var connection = DbContext.Database.GetDbConnection() as SqliteConnection;
        var command = connection!.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;";

        var tables = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                tables.Add(reader.GetString(0));
        }

        Console.WriteLine("Database tables created by EF Core:");
        foreach (var item in tables)
            Console.WriteLine($"  - {item}");

        // Verify expected tables exist
        await Assert.That(tables).Contains("Clips");
        await Assert.That(tables).Contains("ClipData");
        await Assert.That(tables).Contains("BlobTxt");
        await Assert.That(tables).Contains("Collections");
    }

    [Test]
    public async Task SearchAsync_WithTitleQuery_ReturnsMatchingClips()
    {
        // Arrange - Create test clip
        var testClip = new Clip
        {
            Id = Guid.NewGuid(),
            Title = "Test Database Query",
            Creator = "TestUser",
            CapturedAt = DateTimeOffset.UtcNow,
            ContentHash = "test-hash",
            Type = ClipType.Text,
            Del = false,
        };

        DbContext.Clips.Add(testClip);
        await DbContext.SaveChangesAsync();

        // Arrange - Create services
        var clipRepository = new ClipRepository(DbContext, A.Fake<ILogger<ClipRepository>>());
        var mockContextFactory = A.Fake<IDatabaseContextFactory>();
        A.CallTo(() => mockContextFactory.GetClipRepository(A<string>.Ignored)).Returns(clipRepository);
        var mockCollectionService = A.Fake<ICollectionService>();
        A.CallTo(() => mockCollectionService.GetActiveDatabaseKey()).Returns("test-db");
        var mockConfigService = A.Fake<IConfigurationService>();
        A.CallTo(() => mockConfigService.Configuration).Returns(new ClipMateConfiguration());
        var mockSqlValidationService = A.Fake<ISqlValidationService>();
        var searchService = new SearchService(
            mockContextFactory,
            mockCollectionService,
            mockConfigService,
            mockSqlValidationService,
            A.Fake<ILogger<SearchService>>());

        // Act
        var filters = new SearchFilters
        {
            TitleQuery = "Database",
        };

        var results = await searchService.SearchAsync("", filters);

        // Assert
        await Assert.That(results.Clips.Count).IsEqualTo(1);
        await Assert.That(results.Clips[0].Title).IsEqualTo("Test Database Query");
    }

    [Test]
    public async Task BuildSqlQuery_WithTitleFilter_GeneratesValidSQL()
    {
        // Arrange
        var mockContextFactory = A.Fake<IDatabaseContextFactory>();
        var mockCollectionService = A.Fake<ICollectionService>();
        A.CallTo(() => mockCollectionService.GetActiveDatabaseKey()).Returns("test-db");
        var mockConfigService = A.Fake<IConfigurationService>();
        A.CallTo(() => mockConfigService.Configuration).Returns(new ClipMateConfiguration());
        var mockSqlValidationService = A.Fake<ISqlValidationService>();
        var searchService = new SearchService(
            mockContextFactory,
            mockCollectionService,
            mockConfigService,
            mockSqlValidationService,
            A.Fake<ILogger<SearchService>>());

        // Act
        var filters = new SearchFilters
        {
            TitleQuery = "test",
        };

        var sql = searchService.BuildSqlQuery("", filters);

        Console.WriteLine("Generated SQL:");
        Console.WriteLine(sql);

        // Assert
        await Assert.That(sql).Contains("Select DISTINCT Clips.*");
        await Assert.That(sql).Contains("TextSearch(Clips.TITLE,");
        await Assert.That(sql).Contains("Clips.Del = False");
    }

    [Test]
    public async Task TextSearchFunction_WithMatchingText_ReturnsTrue()
    {
        // Arrange - Insert test data directly
        var clipId = Guid.NewGuid();
        var testClip = new Clip
        {
            Id = clipId,
            Title = "Finding Nemo",
            Creator = "Pixar",
            CapturedAt = DateTimeOffset.UtcNow,
            ContentHash = "nemo-hash",
            Type = ClipType.Text,
            Del = false,
        };

        DbContext.Clips.Add(testClip);
        await DbContext.SaveChangesAsync();

        // Act - Execute SQL with TextSearch function
        var connection = DbContext.Database.GetDbConnection() as SqliteConnection;
        var command = connection!.CreateCommand();
        command.CommandText = @"
            SELECT COUNT(*) 
            FROM Clips 
            WHERE TextSearch(Clips.TITLE, 'nemo') = 1";

        var count = (long)(await command.ExecuteScalarAsync())!;

        // Assert
        await Assert.That(count)
            .IsEqualTo(1L)
            .Because("TextSearch function should find 'nemo' in 'Finding Nemo'");
    }
}
