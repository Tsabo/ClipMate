using ClipMate.Core.Models;
using ClipMate.Data.Repositories;
using FakeItEasy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// <see cref="ClipRepository" />'s raw SQL queries that return a limited page of matches with the total match count.
/// </summary>
public class ClipRepositorySqlQueryTests : IntegrationTestBase
{
    private int _probeCalls;

    private ClipRepository CreateRepository()
    {
        // Probe(ID) is true for every row and counts how many rows SQLite evaluated, i.e. how often the query ran
        var connection = (SqliteConnection)DbContext.Database.GetDbConnection();
        connection.CreateFunction("Probe", (string _) =>
        {
            Interlocked.Increment(ref _probeCalls);
            return 1;
        });

        return new ClipRepository(DbContext, A.Fake<ILogger<ClipRepository>>());
    }

    private async Task AddClipsAsync(int count)
    {
        for (var i = 1; i <= count; i++)
        {
            DbContext.Clips.Add(new Clip
            {
                Id = Guid.NewGuid(),
                Title = $"clip {i}",
                SortKey = i,
                CapturedAt = DateTimeOffset.Now,
                ContentHash = Guid.NewGuid().ToString("N"),
                Type = ClipType.Text,
            });
        }

        await DbContext.SaveChangesAsync();
    }

    [Test]
    public async Task ExecuteSearchQueryAsync_MoreMatchesThanTheLimit_RunsTheSearchOnlyOnce()
    {
        // Arrange
        await AddClipsAsync(5);
        var repository = CreateRepository();

        // Act
        var matches = await repository.ExecuteSearchQueryAsync("select Clips.* from Clips where Probe(Clips.ID)", new Dictionary<string, object?>(), 2);

        // Assert
        await Assert.That(matches.TotalMatches).IsEqualTo(5);
        await Assert.That(string.Join(", ", matches.Clips.Select(p => p.Title))).IsEqualTo("clip 5, clip 4");
        await Assert.That(_probeCalls).IsEqualTo(5);
    }

    [Test]
    public async Task ExecuteSearchQueryAsync_NoMatches_ReturnsATotalOfZero()
    {
        // Arrange
        await AddClipsAsync(3);
        var repository = CreateRepository();

        // Act
        var matches = await repository.ExecuteSearchQueryAsync("select Clips.* from Clips where Clips.Title = @Title",
            new Dictionary<string, object?> { ["Title"] = "nothing" }, 10);

        // Assert
        await Assert.That(matches.TotalMatches).IsEqualTo(0);
        await Assert.That(matches.Clips).IsEmpty();
    }

    [Test]
    public async Task ExecuteSearchQueryAsync_LimitOfZero_StillReturnsTheTotal()
    {
        // Arrange
        await AddClipsAsync(3);
        var repository = CreateRepository();

        // Act
        var matches = await repository.ExecuteSearchQueryAsync("select Clips.* from Clips", new Dictionary<string, object?>(), 0);

        // Assert
        await Assert.That(matches.TotalMatches).IsEqualTo(3);
        await Assert.That(matches.Clips).IsEmpty();
    }

    [Test]
    public async Task ExecuteSearchQueryAsync_WithoutALimit_ReturnsEveryMatch()
    {
        // Arrange
        await AddClipsAsync(4);
        var repository = CreateRepository();

        // Act
        var matches = await repository.ExecuteSearchQueryAsync("select Clips.* from Clips;", new Dictionary<string, object?>(), null);

        // Assert
        await Assert.That(matches.TotalMatches).IsEqualTo(4);
        await Assert.That(matches.Clips.Count).IsEqualTo(4);
    }
}
