using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data;
using FakeItEasy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Verifies that closing a database releases its file, so a database removed in Options can be deleted
/// without restarting ClipMate.
/// </summary>
[NotInParallel]
public class DatabaseContextFactoryFileReleaseTests
{
    private string _databasePath = null!;

    [Before(Test)]
    public void Setup() =>
        _databasePath = Path.Combine(Path.GetTempPath(), $"release_{Guid.NewGuid():N}", "clipmate.db");

    [After(Test)]
    public void Cleanup()
    {
        SqliteConnection.ClearAllPools();
        var folder = Path.GetDirectoryName(_databasePath)!;
        if (Directory.Exists(folder))
            Directory.Delete(folder, true);
    }

    private static DatabaseContextFactory CreateFactory(bool cachedWrites)
    {
        var configurationService = A.Fake<IConfigurationService>();
        var configuration = new ClipMateConfiguration();
        configuration.Preferences.EnableCachedDatabaseWrites = cachedWrites;
        A.CallTo(() => configurationService.Configuration).Returns(configuration);

        return new DatabaseContextFactory(A.Fake<IServiceProvider>(), configurationService, A.Fake<ILogger<DatabaseContextFactory>>());
    }

    [Test]
    [Category("Integration")]
    [Arguments(true)]
    [Arguments(false)]
    public async Task CloseDatabase_WithUndisposedContext_ReleasesDatabaseFile(bool cachedWrites)
    {
        // Arrange
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
        using var factory = CreateFactory(cachedWrites);

        // A context that is used but never disposed, as happens with repositories handed out by the factory
        var context = factory.CreateContext(_databasePath);
        await context.Database.EnsureCreatedAsync();
        _ = await context.Clips.CountAsync();

        // Act
        var closed = factory.CloseDatabase(_databasePath);

        // Assert: the database file (and any WAL side files) can be deleted while the app is still running
        await Assert.That(closed).IsTrue();
        foreach (var item in Directory.GetFiles(Path.GetDirectoryName(_databasePath)!))
            File.Delete(item);

        await Assert.That(File.Exists(_databasePath)).IsFalse();
        GC.KeepAlive(context);
    }

    [Test]
    [Category("Integration")]
    public async Task CreateContext_CachedWrites_AppliesWalJournalMode()
    {
        // Arrange
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
        using var factory = CreateFactory(true);
        await using var context = factory.CreateContext(_databasePath);
        await context.Database.EnsureCreatedAsync();

        // Act
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode;";
        var journalMode = (string?)await command.ExecuteScalarAsync();

        // Assert: pragmas are still applied even though the factory no longer opens the connection itself
        await Assert.That(journalMode).IsEqualTo("wal");
    }
}
