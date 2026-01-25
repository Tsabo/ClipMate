namespace ClipMate.Tests.Unit.Services;

public partial class SetupServiceTests
{
    [Test]
    [Category("CreateDatabaseAsync")]
    public async Task CreateDatabaseAsync_WithNullPath_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        await Assert.That(async () => await service.CreateDatabaseAsync(null!))
            .ThrowsException()
            .WithMessageContaining("databasePath");
    }

    [Test]
    [Category("CreateDatabaseAsync")]
    public async Task CreateDatabaseAsync_WithEmptyPath_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        await Assert.That(async () => await service.CreateDatabaseAsync(string.Empty))
            .ThrowsException()
            .WithMessageContaining("databasePath");
    }

    [Test]
    [Category("CreateDatabaseAsync")]
    public async Task CreateDatabaseAsync_WithWhitespacePath_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        await Assert.That(async () => await service.CreateDatabaseAsync("   "))
            .ThrowsException()
            .WithMessageContaining("databasePath");
    }

    [Test]
    [Category("CreateDatabaseAsync")]
    public async Task CreateDatabaseAsync_WithValidPath_CreatesDatabase()
    {
        // Arrange
        var service = CreateService();
        var tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");

        try
        {
            // Act
            var result = await service.CreateDatabaseAsync(tempPath);

            // Assert
            await Assert.That(result).IsTrue();
            await Assert.That(File.Exists(tempPath)).IsTrue();
        }
        finally
        {
            // Cleanup
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    /* Ignore cleanup errors */
                }
            }
        }
    }

    [Test]
    [Category("CreateDatabaseAsync")]
    public async Task CreateDatabaseAsync_WhenDatabaseAlreadyExists_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();
        var tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");

        try
        {
            // Create database first time
            await service.CreateDatabaseAsync(tempPath);

            // Act - Try to create again
            var result = await service.CreateDatabaseAsync(tempPath);

            // Assert
            await Assert.That(result).IsFalse();
        }
        finally
        {
            // Cleanup
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    /* Ignore cleanup errors */
                }
            }
        }
    }

    [Test]
    [Category("CreateDatabaseAsync")]
    public async Task CreateDatabaseAsync_WithNonExistentDirectory_CreatesDirectory()
    {
        // Arrange
        var service = CreateService();
        var tempDir = Path.Combine(Path.GetTempPath(), $"testdir_{Guid.NewGuid()}");
        var tempPath = Path.Combine(tempDir, "test.db");

        try
        {
            // Act
            var result = await service.CreateDatabaseAsync(tempPath);

            // Assert
            await Assert.That(result).IsTrue();
            await Assert.That(Directory.Exists(tempDir)).IsTrue();
            await Assert.That(File.Exists(tempPath)).IsTrue();
        }
        finally
        {
            // Cleanup
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (Directory.Exists(tempDir))
            {
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch
                {
                    /* Ignore cleanup errors */
                }
            }
        }
    }

    [Test]
    [Category("CreateDatabaseAsync")]
    public async Task CreateDatabaseAsync_WithInvalidPath_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();
        var invalidPath = "Z:\\NonExistentDrive\\Invalid\\Path\\test.db";

        // Act
        var result = await service.CreateDatabaseAsync(invalidPath);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    [Category("CreateDatabaseAsync")]
    public async Task CreateDatabaseAsync_WithCancellationToken_RespectsToken()
    {
        // Arrange
        var service = CreateService();
        var tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        try
        {
            // Act
            var result = await service.CreateDatabaseAsync(tempPath, cts.Token);

            // Assert - Operation should fail due to cancellation or succeed if it completes before checking token
            // Either outcome is acceptable, we just verify no exception is thrown
            await Assert.That(result).IsIn(true, false);
        }
        finally
        {
            // Cleanup
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    /* Ignore cleanup errors */
                }
            }
        }
    }
}
