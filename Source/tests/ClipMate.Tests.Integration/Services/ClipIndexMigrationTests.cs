using ClipMate.Data;
using ClipMate.Data.Repositories;
using ClipMate.Data.Services;
using FakeItEasy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// The Clips indexes that let a collection or folder page be read in SortKey order without sorting the whole collection.
/// </summary>
[NotInParallel]
public class ClipIndexMigrationTests
{
    private string _directory = null!;
    private string _databasePath = null!;

    [Before(Test)]
    public void Setup()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"clip_index_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _databasePath = Path.Combine(_directory, "clipmate.db");
    }

    [After(Test)]
    public void Cleanup()
    {
        SqliteConnection.ClearAllPools();
        DatabaseFileCleanup.Delete(_databasePath);
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    [Test]
    public async Task MigrateAsync_DatabaseWithSingleColumnIndexes_ReplacesThemWithCompositeIndexes()
    {
        await CreateDatabaseWithOldClipIndexesAsync();

        await using (var context = CreateContext())
            await new DatabaseSchemaMigrationService().MigrateAsync(context);

        var indexes = await GetClipIndexesAsync();
        await Assert.That(indexes).Contains("IX_Clips_CollectionId_Del_SortKey");
        await Assert.That(indexes).Contains("IX_Clips_FolderId_Del_SortKey");
        await Assert.That(indexes).Contains("IX_Clips_Del");
        await Assert.That(indexes).DoesNotContain("IX_Clips_CollectionId");
        await Assert.That(indexes).DoesNotContain("IX_Clips_FolderId");

        var backups = Directory.GetFiles(Path.Combine(_directory, "MigrationBackups"), "clipmate-migration-*.db");
        await Assert.That(backups).Count().IsEqualTo(1);
    }

    [Test]
    public async Task CollectionPageQuery_AfterMigration_UsesCollectionDelSortKeyIndex()
    {
        await CreateDatabaseWithOldClipIndexesAsync();
        await using (var context = CreateContext())
            await new DatabaseSchemaMigrationService().MigrateAsync(context);

        var recorder = new SqlCommandRecorder();
        await using (var context = CreateContext(recorder))
        {
            var repository = new ClipRepository(context, A.Fake<ILogger<ClipRepository>>());
            await repository.GetPageByCollectionAsync(Guid.NewGuid(), 0, 200);
        }

        var pageQuery = recorder.Commands.Single(p => p.Sql.Contains("LIMIT", StringComparison.OrdinalIgnoreCase) &&
                                                      p.Sql.Contains("FROM \"Clips\"", StringComparison.OrdinalIgnoreCase));

        var plan = await ExplainQueryPlanAsync(pageQuery);
        // Seeks on both CollectionId and Del, and reads SortKey order from the index instead of sorting the collection
        await Assert.That(plan).Contains("USING INDEX IX_Clips_CollectionId_Del_SortKey (CollectionId=? AND Del=?)");
        await Assert.That(plan).DoesNotContain("USE TEMP B-TREE FOR ORDER BY");
    }

    private ClipMateDbContext CreateContext(SqlCommandRecorder? recorder = null)
    {
        var builder = new DbContextOptionsBuilder<ClipMateDbContext>().UseSqlite($"Data Source={_databasePath}");
        if (recorder != null)
            builder.AddInterceptors(recorder);

        return new ClipMateDbContext(builder.Options);
    }

    private async Task<string> ExplainQueryPlanAsync(RecordedSqlCommand query)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN " + query.Sql;
        foreach (var (name, value) in query.Parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);

        var details = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            details.Add(reader.GetString(3));

        return string.Join(Environment.NewLine, details);
    }

    /// <summary>
    /// Creates a database as ClipMate made it before the composite indexes: one index each on CollectionId and FolderId.
    /// </summary>
    private async Task CreateDatabaseWithOldClipIndexesAsync()
    {
        await using (var context = CreateContext())
            await context.Database.EnsureCreatedAsync();

        await ExecuteAsync("""
                           DROP INDEX IF EXISTS IX_Clips_CollectionId_Del_SortKey;
                           DROP INDEX IF EXISTS IX_Clips_FolderId_Del_SortKey;
                           CREATE INDEX IF NOT EXISTS IX_Clips_CollectionId ON Clips (CollectionId);
                           CREATE INDEX IF NOT EXISTS IX_Clips_FolderId ON Clips (FolderId);
                           """);
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private async Task<List<string>> GetClipIndexesAsync()
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'index' AND tbl_name = 'Clips'";

        var indexes = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            indexes.Add(reader.GetString(0));

        return indexes;
    }
}
