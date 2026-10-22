using System.IO;
using ClipMate.Data;
using Microsoft.EntityFrameworkCore;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Pins every table and column the EF model maps. On startup the schema migrator adds any column the model has and a
/// user's database lacks, so a property that is mapped by accident (a transient <c>Clip</c> property missing its
/// <c>Ignore</c>) would silently become a column in every user's database. Changing the schema on purpose means
/// updating Fixtures\DatabaseSchema.approved.txt.
/// </summary>
public class DatabaseModelSnapshotTests
{
    private const string _approvedFileName = "DatabaseSchema.approved.txt";

    [Test]
    public async Task Model_MapsExactlyTheApprovedColumns()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ClipMateDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        await using var context = new ClipMateDbContext(options);

        var approvedPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", _approvedFileName);
        var approved = (await File.ReadAllLinesAsync(approvedPath)).Where(p => p.Length > 0).ToList();

        // Act
        var actual = context.Model.GetEntityTypes()
            .Where(p => p.GetTableName() != null)
            .SelectMany(p => p.GetProperties().Select(c =>
                $"{p.GetTableName()}.{c.GetColumnName()} {c.GetColumnType()}{(c.IsNullable ? "" : " NOT NULL")}"))
            .Order(StringComparer.Ordinal)
            .ToList();

        // Assert
        var added = actual.Except(approved).ToList();
        var removed = approved.Except(actual).ToList();
        if (added.Count > 0 || removed.Count > 0)
        {
            var receivedPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "DatabaseSchema.received.txt");
            await File.WriteAllLinesAsync(receivedPath, actual);

            Assert.Fail($"""
                         The EF model's columns differ from {_approvedFileName}.
                         Added: {(added.Count > 0 ? string.Join(", ", added) : "none")}
                         Removed: {(removed.Count > 0 ? string.Join(", ", removed) : "none")}
                         An in-memory-only property needs Ignore() in ClipMateDbContext. For an intended schema change,
                         replace Source\tests\ClipMate.Tests.Integration\Fixtures\{_approvedFileName} with {receivedPath}.
                         """);
        }
    }
}
