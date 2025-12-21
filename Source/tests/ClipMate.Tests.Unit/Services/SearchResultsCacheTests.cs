using ClipMate.Core.Services;
using ClipMate.Core.ValueObjects;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for SearchResultsCache domain service that manages search result storage per database.
/// </summary>
public class SearchResultsCacheTests
{
    [Test]
    public async Task Constructor_CreatesEmptyCache()
    {
        // Arrange & Act
        var cache = new SearchResultsCache();

        // Assert
        await Assert.That(cache.HasResults("db1")).IsFalse();
    }

    [Test]
    public async Task SetResults_StoresResultsForDatabase()
    {
        // Arrange
        var cache = new SearchResultsCache();
        var clipIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var searchResult = new SearchResult("test query", clipIds);

        // Act
        cache.SetResults("db1", searchResult);

        // Assert
        await Assert.That(cache.HasResults("db1")).IsTrue();
        var result = cache.GetResults("db1");
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Query).IsEqualTo("test query");
        await Assert.That(result.ClipIds.Count).IsEqualTo(2);
    }

    [Test]
    public async Task GetResults_ReturnsNullForUnknownDatabase()
    {
        // Arrange
        var cache = new SearchResultsCache();

        // Act
        var result = cache.GetResults("unknown");

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ClearResults_RemovesResultsForDatabase()
    {
        // Arrange
        var cache = new SearchResultsCache();
        var clipIds = new List<Guid> { Guid.NewGuid() };
        var searchResult = new SearchResult("test", clipIds);
        cache.SetResults("db1", searchResult);

        // Act
        cache.ClearResults("db1");

        // Assert
        await Assert.That(cache.HasResults("db1")).IsFalse();
    }

    [Test]
    public async Task SetResults_OverwritesPreviousResults()
    {
        // Arrange
        var cache = new SearchResultsCache();
        var clipIds1 = new List<Guid> { Guid.NewGuid() };
        var clipIds2 = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var result1 = new SearchResult("query1", clipIds1);
        var result2 = new SearchResult("query2", clipIds2);

        // Act
        cache.SetResults("db1", result1);
        cache.SetResults("db1", result2);

        // Assert
        var current = cache.GetResults("db1");
        await Assert.That(current!.Query).IsEqualTo("query2");
        await Assert.That(current.ClipIds.Count).IsEqualTo(2);
    }

    [Test]
    public async Task MultipleDatabase_IndependentCaches()
    {
        // Arrange
        var cache = new SearchResultsCache();
        var clipIds1 = new List<Guid> { Guid.NewGuid() };
        var clipIds2 = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var result1 = new SearchResult("db1 query", clipIds1);
        var result2 = new SearchResult("db2 query", clipIds2);

        // Act
        cache.SetResults("db1", result1);
        cache.SetResults("db2", result2);

        // Assert
        await Assert.That(cache.HasResults("db1")).IsTrue();
        await Assert.That(cache.HasResults("db2")).IsTrue();

        var db1Result = cache.GetResults("db1");
        var db2Result = cache.GetResults("db2");

        await Assert.That(db1Result!.Query).IsEqualTo("db1 query");
        await Assert.That(db2Result!.Query).IsEqualTo("db2 query");
    }

    [Test]
    public async Task ClearAll_RemovesAllCachedResults()
    {
        // Arrange
        var cache = new SearchResultsCache();
        cache.SetResults("db1", new SearchResult("q1", [Guid.NewGuid()]));
        cache.SetResults("db2", new SearchResult("q2", [Guid.NewGuid()]));

        // Act
        cache.ClearAll();

        // Assert
        await Assert.That(cache.HasResults("db1")).IsFalse();
        await Assert.That(cache.HasResults("db2")).IsFalse();
    }
}
