using System.IO.Compression;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Data;
using ClipMate.Data.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClipMate.Tests.Integration.Scenarios;

/// <summary>
/// End-to-end scenario tests for database backup and restore workflows.
/// </summary>
public class BackupRestoreScenarioTests : IntegrationTestBase
{
    private string _backupDirectory = null!;
    private IDbContextFactory<ClipMateDbContext> _contextFactory = null!;
    private IDatabaseMaintenanceService _service = null!;
    private string _testDirectory = null!;

    [Before(Test)]
    public new async Task SetupAsync()
    {
        await base.SetupAsync();

        _testDirectory = Path.Combine(Path.GetTempPath(), $"ClipMateTest_{Guid.NewGuid():N}");
        _backupDirectory = Path.Combine(_testDirectory, "Backups");
        Directory.CreateDirectory(_testDirectory);
        Directory.CreateDirectory(_backupDirectory);

        var options = new DbContextOptionsBuilder<ClipMateDbContext>()
            .UseSqlite($"DataSource={Path.Combine(_testDirectory, "test.db")}")
            .Options;

        _contextFactory = new TestDbContextFactory(options);

        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
        }

        using var loggerFactory = new LoggerFactory();
        var logger = loggerFactory.CreateLogger<DatabaseMaintenanceService>();
        _service = new DatabaseMaintenanceService(logger);
    }

    [After(Test)]
    public new async Task CleanupAsync()
    {
        await base.CleanupAsync();

        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, true);
            }
            catch { }
        }
    }

    [Test]
    public async Task Scenario_CreateBackup_RestoreFromBackup_DataPreserved()
    {
        // Arrange - Create initial data
        var config = new DatabaseConfiguration
        {
            Name = "clipmate",
            FilePath = Path.Combine(_testDirectory, "test.db"),
            BackupDirectory = _backupDirectory,
        };

        var originalClipId = Guid.Empty;
        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            var clip = new Clip
            {
                Id = Guid.NewGuid(),
                Title = "Important Data",
                CapturedAt = DateTime.UtcNow,
                ContentHash = "hash123",
                Type = ClipType.Text,
            };

            context.Clips.Add(clip);
            await context.SaveChangesAsync();
            originalClipId = clip.Id;
        }

        // Act - Backup
        var backupPath = await _service.BackupDatabaseAsync(config, _backupDirectory);

        // Modify database after backup
        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            var clip = new Clip
            {
                Title = "Modified After Backup",
                CapturedAt = DateTime.UtcNow,
                ContentHash = "hash456",
                Type = ClipType.Text,
            };

            context.Clips.Add(clip);
            await context.SaveChangesAsync();
        }

        // Ensure all connections are closed before restore
        SqliteConnection.ClearAllPools();
        await Task.Delay(100);

        // Act - Restore
        await _service.RestoreDatabaseAsync(backupPath, config);

        // Assert - Data should match backup state
        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            var clips = await context.Clips.ToListAsync();
            await Assert.That(clips.Count).IsEqualTo(1);
            await Assert.That(clips[0].Id).IsEqualTo(originalClipId);
            await Assert.That(clips[0].Title).IsEqualTo("Important Data");
        }
    }

    [Test]
    public async Task Scenario_MultipleBackups_AllAccessible()
    {
        // Arrange
        var config = new DatabaseConfiguration
        {
            Name = "clipmate",
            FilePath = Path.Combine(_testDirectory, "test.db"),
            BackupDirectory = _backupDirectory,
        };

        // Act - Create multiple backups over time
        var backups = new List<string>();

        for (var i = 0; i < 3; i++)
        {
            await using (var context = await _contextFactory.CreateDbContextAsync())
            {
                context.Clips.Add(new Clip
                {
                    Title = $"Clip {i}",
                    CapturedAt = DateTime.UtcNow,
                    ContentHash = $"hash{i}",
                    Type = ClipType.Text,
                });

                await context.SaveChangesAsync();
            }

            var backupPath = await _service.BackupDatabaseAsync(config, _backupDirectory);
            backups.Add(backupPath);

            // Ensure connections are closed and different timestamps for next backup (filenames include seconds)
            SqliteConnection.ClearAllPools();
            await Task.Delay(1100); // Ensure different timestamps (backup filename format includes seconds)
        }

        // Assert - All backups exist
        await Assert.That(backups.Count).IsEqualTo(3);
        foreach (var backup in backups)
            await Assert.That(File.Exists(backup)).IsTrue();

        // Verify each backup can be opened
        foreach (var backup in backups)
        {
            using var archive = ZipFile.OpenRead(backup);
            await Assert.That(archive.Entries.Count).IsGreaterThan(0);
        }
    }

    [Test]
    public async Task Scenario_BackupWithCleanup_OldDataRemoved()
    {
        // Arrange
        var config = new DatabaseConfiguration
        {
            Name = "clipmate",
            FilePath = Path.Combine(_testDirectory, "test.db"),
            PurgeDays = 7,
        };

        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            // Add old clip
            context.Clips.Add(new Clip
            {
                Title = "Old Clip",
                CapturedAt = DateTime.UtcNow.AddDays(-30),
                Del = true,
                DelDate = DateTime.UtcNow.AddDays(-30),
                ContentHash = "old",
                Type = ClipType.Text,
            });

            // Add recent clip
            context.Clips.Add(new Clip
            {
                Title = "Recent Clip",
                CapturedAt = DateTime.UtcNow,
                ContentHash = "recent",
                Type = ClipType.Text,
            });

            await context.SaveChangesAsync();
        }

        // Act - Run cleanup then backup
        var purgedCount = await _service.RunCleanupAsync(config);

        // Wait for SQLite to release file locks
        await Task.Delay(200);
        GC.Collect();
        GC.WaitForPendingFinalizers();

        var backupPath = await _service.BackupDatabaseAsync(config, _backupDirectory);

        // Assert
        await Assert.That(purgedCount).IsEqualTo(1);
        await Assert.That(File.Exists(backupPath)).IsTrue();

        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            var clips = await context.Clips.ToListAsync();
            await Assert.That(clips.Count).IsEqualTo(1);
            await Assert.That(clips[0].Title).IsEqualTo("Recent Clip");
        }
    }

    [Test]
    public async Task Scenario_EmptyTrash_ThenBackup_NoDeletedClips()
    {
        // Arrange
        var config = new DatabaseConfiguration
        {
            Name = "clipmate",
            FilePath = Path.Combine(_testDirectory, "test.db"),
        };

        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            context.Clips.Add(new Clip
            {
                Title = "Active Clip",
                CapturedAt = DateTime.UtcNow,
                Del = false,
                ContentHash = "active",
                Type = ClipType.Text,
            });

            context.Clips.Add(new Clip
            {
                Title = "Deleted Clip",
                CapturedAt = DateTime.UtcNow,
                Del = true,
                DelDate = DateTime.UtcNow,
                ContentHash = "deleted",
                Type = ClipType.Text,
            });

            await context.SaveChangesAsync();
        }

        // Act
        var deletedCount = await _service.EmptyTrashAsync(config);

        // Wait for SQLite to release file locks
        await Task.Delay(200);
        GC.Collect();
        GC.WaitForPendingFinalizers();

        var backupPath = await _service.BackupDatabaseAsync(config, _backupDirectory);

        // Assert
        await Assert.That(deletedCount).IsEqualTo(1);
        await Assert.That(File.Exists(backupPath)).IsTrue();

        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            var allClips = await context.Clips.ToListAsync();
            await Assert.That(allClips.Count).IsEqualTo(1);
            await Assert.That(allClips[0].Title).IsEqualTo("Active Clip");
            await Assert.That(allClips[0].Del).IsFalse();
        }
    }

    [Test]
    public async Task Scenario_RepairDatabase_DataIntact()
    {
        // Arrange
        var config = new DatabaseConfiguration
        {
            Name = "clipmate",
            FilePath = Path.Combine(_testDirectory, "test.db"),
        };

        var originalClipId = Guid.Empty;
        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            var clip = new Clip
            {
                Title = "Test Data",
                CapturedAt = DateTime.UtcNow,
                ContentHash = "test",
                Type = ClipType.Text,
            };

            context.Clips.Add(clip);
            await context.SaveChangesAsync();
            originalClipId = clip.Id;
        }

        // Act - Simple repair (VACUUM)
        await _service.RepairDatabaseAsync(config);

        // Assert - Data still intact
        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            var clips = await context.Clips.ToListAsync();
            await Assert.That(clips.Count).IsEqualTo(1);
            await Assert.That(clips[0].Id).IsEqualTo(originalClipId);
            await Assert.That(clips[0].Title).IsEqualTo("Test Data");
        }
    }

    private class TestDbContextFactory : IDbContextFactory<ClipMateDbContext>
    {
        private readonly DbContextOptions<ClipMateDbContext> _options;

        public TestDbContextFactory(DbContextOptions<ClipMateDbContext> options)
        {
            _options = options;
        }

        public ClipMateDbContext CreateDbContext() => new(_options);
    }
}
