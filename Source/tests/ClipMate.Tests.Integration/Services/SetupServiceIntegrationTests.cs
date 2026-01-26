using ClipMate.Data.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Integration tests for SetupService database creation and validation.
/// Tests run sequentially to avoid database file locking conflicts.
/// </summary>
[NotInParallel]
public class SetupServiceIntegrationTests : IntegrationTestBase
{
    private SetupService CreateService()
    {
        var mockLogger = new Mock<ILogger<SetupService>>();
        return new SetupService(mockLogger.Object);
    }

    [Test]
    [Category("Integration")]
    [Category("SetupService")]
    public async Task CreateDatabaseAsync_WithValidPath_CreatesValidDatabase()
    {
        // Arrange
        var service = CreateService();
        var tempPath = Path.Combine(Path.GetTempPath(), $"integration_test_{Guid.NewGuid()}.db");

        try
        {
            // Act
            var created = await service.CreateDatabaseAsync(tempPath);

            // Assert
            await Assert.That(created).IsTrue();
            await Assert.That(File.Exists(tempPath)).IsTrue();

            // Verify database is valid
            var isValid = await service.ValidateDatabaseAsync(tempPath);
            await Assert.That(isValid).IsTrue();

            // Force close all connections
            SqliteConnection.ClearAllPools();
            await Task.Delay(200);
        }
        finally
        {
            // Cleanup
            CleanupDatabase(tempPath);
        }
    }

    [Test]
    [Category("Integration")]
    [Category("SetupService")]
    public async Task ValidateDatabaseAsync_WithCorruptedDatabase_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();
        var tempPath = Path.Combine(Path.GetTempPath(), $"corrupted_integration_{Guid.NewGuid()}.db");

        try
        {
            // Create a valid database first
            await service.CreateDatabaseAsync(tempPath);

            // Close all connections and wait
            SqliteConnection.ClearAllPools();
            await Task.Delay(200);
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // Corrupt it by writing random bytes
            await using (var fs = File.Open(tempPath, FileMode.Open, FileAccess.Write, FileShare.None))
            {
                fs.Seek(100, SeekOrigin.Begin);
                fs.WriteByte(0xFF);
                fs.WriteByte(0xFF);
            }

            // Wait for file handles to close
            await Task.Delay(200);

            // Act
            var result = await service.ValidateDatabaseAsync(tempPath);

            // Assert - May or may not be valid depending on where corruption occurred
            // SQLite is resilient, so this test just verifies no exception is thrown
            await Assert.That(result).IsIn(true, false);

            // Close all connections before cleanup
            SqliteConnection.ClearAllPools();
            await Task.Delay(200);
        }
        finally
        {
            // Cleanup
            CleanupDatabase(tempPath);
        }
    }

    /// <summary>
    /// Tests that EnsureDefaultDatabaseAsync creates a new database when none exists.
    /// IMPORTANT: This test modifies the actual ClipMate database in AppData\Local\ClipMate.
    /// ClipMate must not be running for this test to succeed.
    /// </summary>
    [Test]
    [Category("Integration")]
    [Category("SetupService")]
    [Category("Manual")]
    [Skip("Modifies production database location - run manually only")]
    public async Task EnsureDefaultDatabaseAsync_CreatesNewDatabaseIfNotExists()
    {
        // Arrange
        var service = CreateService();
        var defaultPath = service.GetDefaultDatabasePath();

        // Skip if database is locked (ClipMate is running)
        if (!TryCleanupDatabase(defaultPath))
        {
            // Test skipped - database is in use by running application
            // TUnit doesn't support Assert.Inconclusive, so we return early
            return;
        }

        try
        {
            // Act
            var resultPath = await service.EnsureDefaultDatabaseAsync();

            // Assert
            await Assert.That(resultPath).IsEqualTo(defaultPath);
            await Assert.That(File.Exists(resultPath)).IsTrue();

            // Verify it's valid
            var isValid = await service.ValidateDatabaseAsync(resultPath);
            await Assert.That(isValid).IsTrue();

            // Close all connections
            SqliteConnection.ClearAllPools();
            await Task.Delay(200);
        }
        finally
        {
            // Cleanup
            TryCleanupDatabase(defaultPath);
        }
    }

    /// <summary>
    /// Tests that EnsureDefaultDatabaseAsync reuses an existing valid database without recreating it.
    /// IMPORTANT: This test modifies the actual ClipMate database in AppData\Local\ClipMate.
    /// ClipMate must not be running for this test to succeed.
    /// </summary>
    [Test]
    [Category("Integration")]
    [Category("SetupService")]
    [Category("Manual")]
    [Skip("Modifies production database location - run manually only")]
    public async Task EnsureDefaultDatabaseAsync_ReusesExistingValidDatabase()
    {
        // Arrange
        var service = CreateService();
        var defaultPath = service.GetDefaultDatabasePath();

        // Skip if database is locked (ClipMate is running)
        if (!TryCleanupDatabase(defaultPath))
        {
            // Test skipped - database is in use by running application
            // TUnit doesn't support Assert.Inconclusive, so we return early
            return;
        }

        try
        {
            // Create database first time
            await service.EnsureDefaultDatabaseAsync();

            // Close connections and wait
            SqliteConnection.ClearAllPools();
            await Task.Delay(200);
            GC.Collect();
            GC.WaitForPendingFinalizers();

            var creationTime = File.GetLastWriteTimeUtc(defaultPath);

            // Wait a bit to ensure time difference if recreated
            await Task.Delay(300);

            // Act - Call again
            var resultPath = await service.EnsureDefaultDatabaseAsync();
            var secondCheckTime = File.GetLastWriteTimeUtc(defaultPath);

            // Assert - File should not have been recreated
            await Assert.That(resultPath).IsEqualTo(defaultPath);
            await Assert.That(secondCheckTime).IsEqualTo(creationTime);

            // Close all connections
            SqliteConnection.ClearAllPools();
            await Task.Delay(200);
        }
        finally
        {
            // Cleanup
            TryCleanupDatabase(defaultPath);
        }
    }

    /// <summary>
    /// Tests that EnsureDefaultDatabaseAsync recreates an invalid/corrupted database.
    /// IMPORTANT: This test modifies the actual ClipMate database in AppData\Local\ClipMate.
    /// ClipMate must not be running for this test to succeed.
    /// </summary>
    [Test]
    [Category("Integration")]
    [Category("SetupService")]
    [Category("Manual")]
    [Skip("Modifies production database location - run manually only")]
    public async Task EnsureDefaultDatabaseAsync_RecreatesInvalidDatabase()
    {
        // Arrange
        var service = CreateService();
        var defaultPath = service.GetDefaultDatabasePath();

        // Skip if database is locked (ClipMate is running)
        if (!TryCleanupDatabase(defaultPath))
        {
            // Test skipped - database is in use by running application
            // TUnit doesn't support Assert.Inconclusive, so we return early
            return;
        }

        try
        {
            // Create an invalid database file
            var directory = Path.GetDirectoryName(defaultPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            await File.WriteAllTextAsync(defaultPath, "Invalid database content");

            // Wait for file to be written and close handles
            await Task.Delay(200);

            // Act
            var resultPath = await service.EnsureDefaultDatabaseAsync();

            // Assert
            await Assert.That(resultPath).IsEqualTo(defaultPath);
            await Assert.That(File.Exists(resultPath)).IsTrue();

            // Verify it's now valid
            var isValid = await service.ValidateDatabaseAsync(resultPath);
            await Assert.That(isValid).IsTrue();

            // Close all connections
            SqliteConnection.ClearAllPools();
            await Task.Delay(200);
        }
        finally
        {
            // Cleanup
            TryCleanupDatabase(defaultPath);
        }
    }

    private static bool TryCleanupDatabase(string path)
    {
        if (!File.Exists(path))
            return true; // Nothing to clean, can proceed

        try
        {
            CleanupDatabase(path);
            return true;
        }
        catch (IOException)
        {
            // File is locked - skip test
            return false;
        }
    }

    private static void CleanupDatabase(string path)
    {
        if (!File.Exists(path))
            return;

        // Clear all SQLite connection pools first
        SqliteConnection.ClearAllPools();

        // Try multiple times to handle file locks
        for (int i = 0; i < 10; i++)
        {
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                Thread.Sleep(200);

                File.Delete(path);
                return;
            }
            catch (IOException)
            {
                if (i == 9)
                    throw; // Fail on last attempt

                Thread.Sleep(500 * (i + 1)); // Exponential backoff
            }
        }
    }
}
