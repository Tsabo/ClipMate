using ClipMate.Core.Models;
using ClipMate.Data;
using ClipMate.Data.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Unit tests for ShortcutService (Test-Driven Development).
/// </summary>
public class ShortcutServiceTests
{
    private const string _testDatabaseKey = "test_db.db";
    private readonly Mock<IDatabaseManager> _mockDatabaseManager;
    private readonly Mock<ILogger<ShortcutService>> _mockLogger;
    private SqliteConnection _connection = null!;
    private DbContextOptions<ClipMateDbContext> _contextOptions = null!;

    public ShortcutServiceTests()
    {
        _mockDatabaseManager = new Mock<IDatabaseManager>();
        _mockLogger = new Mock<ILogger<ShortcutService>>();
    }

    /// <summary>
    /// Creates a new DbContext using the shared connection.
    /// Each context can be disposed independently while sharing the same database.
    /// </summary>
    private ClipMateDbContext CreateContext() => new(_contextOptions);

    [Before(Test)]
    public void Setup()
    {
        // Create in-memory database for testing
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _contextOptions = new DbContextOptionsBuilder<ClipMateDbContext>()
            .UseSqlite(_connection)
            .Options;

        // Initialize database schema
        using (var context = CreateContext())
        {
            context.Database.EnsureCreated();
        }

        // Setup database manager to return a new context each time
        // This allows ShortcutService to dispose contexts without affecting other operations
        _mockDatabaseManager.Setup(p => p.CreateDatabaseContext(_testDatabaseKey))
            .Returns(() => CreateContext());
    }

    [After(Test)]
    public void Cleanup()
    {
        _connection?.Close();
        _connection?.Dispose();
    }

    [Test]
    public async Task GetAllAsync_WithNoShortcuts_ShouldReturnEmptyList()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.GetAllAsync(_testDatabaseKey);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetAllAsync_WithShortcuts_ShouldReturnOrderedByNickname()
    {
        // Arrange
        var clip1 = CreateTestClip();
        var clip2 = CreateTestClip();
        var clip3 = CreateTestClip();
        await using (var setupContext = CreateContext())
        {
            await setupContext.Clips.AddRangeAsync(clip1, clip2, clip3);
            await setupContext.SaveChangesAsync();

            var shortcuts = new[]
            {
                CreateTestShortcut(clip1.Id, ".z.last"),
                CreateTestShortcut(clip2.Id, ".a.first"),
                CreateTestShortcut(clip3.Id, ".m.middle"),
            };

            await setupContext.Shortcuts.AddRangeAsync(shortcuts);
            await setupContext.SaveChangesAsync();
        }

        var service = CreateService();

        // Act
        var result = await service.GetAllAsync(_testDatabaseKey);

        // Assert
        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(result[0].Nickname).IsEqualTo(".a.first");
        await Assert.That(result[1].Nickname).IsEqualTo(".m.middle");
        await Assert.That(result[2].Nickname).IsEqualTo(".z.last");
    }

    [Test]
    public async Task GetByNicknamePrefixAsync_WithEmptyPrefix_ShouldReturnEmptyList()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.GetByNicknamePrefixAsync(_testDatabaseKey, string.Empty);

        // Assert
        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetByNicknamePrefixAsync_WithMatchingPrefix_ShouldReturnFilteredShortcuts()
    {
        // Arrange
        var clip1 = CreateTestClip();
        var clip2 = CreateTestClip();
        var clip3 = CreateTestClip();
        await using (var setupContext = CreateContext())
        {
            await setupContext.Clips.AddRangeAsync(clip1, clip2, clip3);
            await setupContext.SaveChangesAsync();

            var shortcuts = new[]
            {
                CreateTestShortcut(clip1.Id, ".cc.v.number"),
                CreateTestShortcut(clip2.Id, ".cc.m.date"),
                CreateTestShortcut(clip3.Id, ".sig.work"),
            };

            await setupContext.Shortcuts.AddRangeAsync(shortcuts);
            await setupContext.SaveChangesAsync();
        }

        var service = CreateService();

        // Act
        var result = await service.GetByNicknamePrefixAsync(_testDatabaseKey, ".cc");

        // Assert
        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result[0].Nickname).IsEqualTo(".cc.m.date");
        await Assert.That(result[1].Nickname).IsEqualTo(".cc.v.number");
    }

    [Test]
    public async Task GetByNicknamePrefixAsync_ShouldBeCaseInsensitive()
    {
        // Arrange
        var clip = CreateTestClip();
        await using (var setupContext = CreateContext())
        {
            await setupContext.Clips.AddAsync(clip);
            await setupContext.SaveChangesAsync();

            var shortcut = CreateTestShortcut(clip.Id, ".CC.Visa");
            await setupContext.Shortcuts.AddAsync(shortcut);
            await setupContext.SaveChangesAsync();
        }

        var service = CreateService();

        // Act
        var result = await service.GetByNicknamePrefixAsync(_testDatabaseKey, ".cc");

        // Assert
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].Nickname).IsEqualTo(".CC.Visa");
    }

    [Test]
    public async Task GetByClipIdAsync_WithExistingShortcut_ShouldReturnShortcut()
    {
        // Arrange
        var clip = CreateTestClip();
        await using (var setupContext = CreateContext())
        {
            await setupContext.Clips.AddAsync(clip);
            await setupContext.SaveChangesAsync();

            var shortcut = CreateTestShortcut(clip.Id, ".test");
            await setupContext.Shortcuts.AddAsync(shortcut);
            await setupContext.SaveChangesAsync();
        }

        var service = CreateService();

        // Act
        var result = await service.GetByClipIdAsync(_testDatabaseKey, clip.Id);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.ClipId).IsEqualTo(clip.Id);
        await Assert.That(result.Nickname).IsEqualTo(".test");
    }

    [Test]
    public async Task GetByClipIdAsync_WithNoShortcut_ShouldReturnNull()
    {
        // Arrange
        var clip = CreateTestClip();
        await using (var setupContext = CreateContext())
        {
            await setupContext.Clips.AddAsync(clip);
            await setupContext.SaveChangesAsync();
        }

        var service = CreateService();

        // Act
        var result = await service.GetByClipIdAsync(_testDatabaseKey, clip.Id);

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task UpdateClipShortcutAsync_WithNewShortcut_ShouldCreateShortcut()
    {
        // Arrange
        var clip = CreateTestClip();
        await using (var setupContext = CreateContext())
        {
            await setupContext.Clips.AddAsync(clip);
            await setupContext.SaveChangesAsync();
        }

        var service = CreateService();

        // Act
        await service.UpdateClipShortcutAsync(_testDatabaseKey, clip.Id, ".test");

        // Assert
        await using (var verifyContext = CreateContext())
        {
            var shortcuts = await verifyContext.Shortcuts.ToListAsync();
            await Assert.That(shortcuts.Count).IsEqualTo(1);
            await Assert.That(shortcuts[0].ClipId).IsEqualTo(clip.Id);
            await Assert.That(shortcuts[0].Nickname).IsEqualTo(".test");
            await Assert.That(shortcuts[0].ClipGuid).IsEqualTo(clip.Id);
        }
    }

    [Test]
    public async Task UpdateClipShortcutAsync_WithExistingShortcut_ShouldUpdateNickname()
    {
        // Arrange
        var clip = CreateTestClip();
        await using (var setupContext = CreateContext())
        {
            await setupContext.Clips.AddAsync(clip);
            await setupContext.SaveChangesAsync();

            var shortcut = CreateTestShortcut(clip.Id, ".old");
            await setupContext.Shortcuts.AddAsync(shortcut);
            await setupContext.SaveChangesAsync();
        }

        var service = CreateService();

        // Act
        await service.UpdateClipShortcutAsync(_testDatabaseKey, clip.Id, ".new");

        // Assert
        await using (var verifyContext = CreateContext())
        {
            var shortcuts = await verifyContext.Shortcuts.ToListAsync();
            await Assert.That(shortcuts.Count).IsEqualTo(1);
            await Assert.That(shortcuts[0].Nickname).IsEqualTo(".new");
        }
    }

    [Test]
    public async Task UpdateClipShortcutAsync_WithNullNickname_ShouldDeleteShortcut()
    {
        // Arrange
        var clip = CreateTestClip();
        await using (var setupContext = CreateContext())
        {
            await setupContext.Clips.AddAsync(clip);
            await setupContext.SaveChangesAsync();

            var shortcut = CreateTestShortcut(clip.Id, ".test");
            await setupContext.Shortcuts.AddAsync(shortcut);
            await setupContext.SaveChangesAsync();
        }

        var service = CreateService();

        // Act
        await service.UpdateClipShortcutAsync(_testDatabaseKey, clip.Id, null);

        // Assert
        await using (var verifyContext = CreateContext())
        {
            var shortcuts = await verifyContext.Shortcuts.ToListAsync();
            await Assert.That(shortcuts.Count).IsEqualTo(0);
        }
    }

    [Test]
    public async Task UpdateClipShortcutAsync_WithEmptyNickname_ShouldDeleteShortcut()
    {
        // Arrange
        var clip = CreateTestClip();
        await using (var setupContext = CreateContext())
        {
            await setupContext.Clips.AddAsync(clip);
            await setupContext.SaveChangesAsync();

            var shortcut = CreateTestShortcut(clip.Id, ".test");
            await setupContext.Shortcuts.AddAsync(shortcut);
            await setupContext.SaveChangesAsync();
        }

        var service = CreateService();

        // Act
        await service.UpdateClipShortcutAsync(_testDatabaseKey, clip.Id, "   ");

        // Assert
        await using (var verifyContext = CreateContext())
        {
            var shortcuts = await verifyContext.Shortcuts.ToListAsync();
            await Assert.That(shortcuts.Count).IsEqualTo(0);
        }
    }

    [Test]
    public async Task UpdateClipShortcutAsync_WithNicknameTooLong_ShouldThrowArgumentException()
    {
        // Arrange
        var clip = CreateTestClip();
        await using (var setupContext = CreateContext())
        {
            await setupContext.Clips.AddAsync(clip);
            await setupContext.SaveChangesAsync();
        }

        var service = CreateService();
        var longNickname = new string('a', 65); // 65 characters

        // Act & Assert
        await Assert.That(async () => await service.UpdateClipShortcutAsync(_testDatabaseKey, clip.Id, longNickname))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task DeleteAsync_WithExistingShortcut_ShouldRemoveShortcut()
    {
        // Arrange
        var clip = CreateTestClip();
        Shortcut shortcut;
        await using (var setupContext = CreateContext())
        {
            await setupContext.Clips.AddAsync(clip);
            await setupContext.SaveChangesAsync();

            shortcut = CreateTestShortcut(clip.Id, ".test");
            await setupContext.Shortcuts.AddAsync(shortcut);
            await setupContext.SaveChangesAsync();
        }

        var service = CreateService();

        // Act
        await service.DeleteAsync(_testDatabaseKey, shortcut.Id);

        // Assert
        await using (var verifyContext = CreateContext())
        {
            var shortcuts = await verifyContext.Shortcuts.ToListAsync();
            await Assert.That(shortcuts.Count).IsEqualTo(0);
        }
    }

    [Test]
    public async Task DeleteAsync_WithNonExistentShortcut_ShouldNotThrow()
    {
        // Arrange
        var service = CreateService();
        var nonExistentId = Guid.NewGuid();

        // Act & Assert (should not throw)
        await service.DeleteAsync(_testDatabaseKey, nonExistentId);

        await using (var verifyContext = CreateContext())
        {
            var shortcuts = await verifyContext.Shortcuts.ToListAsync();
            await Assert.That(shortcuts.Count).IsEqualTo(0);
        }
    }

    [Test]
    public async Task DeleteByClipIdAsync_WithMultipleShortcuts_ShouldRemoveAll()
    {
        // Arrange
        var clip = CreateTestClip();
        await using (var setupContext = CreateContext())
        {
            await setupContext.Clips.AddAsync(clip);
            await setupContext.SaveChangesAsync();

            var shortcuts = new[]
            {
                CreateTestShortcut(clip.Id, ".test1"),
                CreateTestShortcut(clip.Id, ".test2"),
            };

            await setupContext.Shortcuts.AddRangeAsync(shortcuts);
            await setupContext.SaveChangesAsync();
        }

        var service = CreateService();

        // Act
        await service.DeleteByClipIdAsync(_testDatabaseKey, clip.Id);

        // Assert
        await using (var verifyContext = CreateContext())
        {
            var remainingShortcuts = await verifyContext.Shortcuts.ToListAsync();
            await Assert.That(remainingShortcuts.Count).IsEqualTo(0);
        }
    }

    [Test]
    public async Task DeleteByClipIdAsync_WithNoShortcuts_ShouldNotThrow()
    {
        // Arrange
        var clip = CreateTestClip();
        await using (var setupContext = CreateContext())
        {
            await setupContext.Clips.AddAsync(clip);
            await setupContext.SaveChangesAsync();
        }

        var service = CreateService();

        // Act & Assert (should not throw)
        await service.DeleteByClipIdAsync(_testDatabaseKey, clip.Id);

        await using (var verifyContext = CreateContext())
        {
            var shortcuts = await verifyContext.Shortcuts.ToListAsync();
            await Assert.That(shortcuts.Count).IsEqualTo(0);
        }
    }

    private ShortcutService CreateService() => new(_mockDatabaseManager.Object, _mockLogger.Object);

    private static Clip CreateTestClip()
    {
        var clipId = Guid.NewGuid();
        return new Clip
        {
            Id = clipId,
            Title = $"Test Clip {clipId}",
            CapturedAt = DateTimeOffset.UtcNow,
            SortKey = 100,
            Size = 0,
            Locale = 0,
            ContentHash = clipId.ToString()[..32], // 32 chars for hash
        };
    }

    private static Shortcut CreateTestShortcut(Guid clipId, string nickname) =>
        new()
        {
            Id = Guid.NewGuid(),
            ClipId = clipId,
            Nickname = nickname,
            ClipGuid = clipId,
        };
}
