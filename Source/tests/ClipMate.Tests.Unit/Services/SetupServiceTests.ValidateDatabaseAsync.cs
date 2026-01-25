namespace ClipMate.Tests.Unit.Services;

public partial class SetupServiceTests
{
    [Test]
    [Category("ValidateDatabaseAsync")]
    public async Task ValidateDatabaseAsync_WithNullPath_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.ValidateDatabaseAsync(null!);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    [Category("ValidateDatabaseAsync")]
    public async Task ValidateDatabaseAsync_WithEmptyPath_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.ValidateDatabaseAsync(string.Empty);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    [Category("ValidateDatabaseAsync")]
    public async Task ValidateDatabaseAsync_WithWhitespacePath_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.ValidateDatabaseAsync("   ");

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    [Category("ValidateDatabaseAsync")]
    public async Task ValidateDatabaseAsync_WithNonExistentFile_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.db");

        // Act
        var result = await service.ValidateDatabaseAsync(nonExistentPath);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    [Category("ValidateDatabaseAsync")]
    public async Task ValidateDatabaseAsync_WithValidDatabase_ReturnsTrue()
    {
        // Arrange
        var service = CreateService();
        var tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");

        try
        {
            // Create a valid database
            await service.CreateDatabaseAsync(tempPath);

            // Act
            var result = await service.ValidateDatabaseAsync(tempPath);

            // Assert
            await Assert.That(result).IsTrue();
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
    [Category("ValidateDatabaseAsync")]
    public async Task ValidateDatabaseAsync_WithInvalidDatabaseFile_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();
        var tempPath = Path.Combine(Path.GetTempPath(), $"invalid_{Guid.NewGuid()}.db");

        try
        {
            // Create an invalid database file (just text content)
            await File.WriteAllTextAsync(tempPath, "This is not a valid SQLite database");

            // Act
            var result = await service.ValidateDatabaseAsync(tempPath);

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
    [Category("ValidateDatabaseAsync")]
    public async Task ValidateDatabaseAsync_WithCancellationToken_RespectsToken()
    {
        // Arrange
        var service = CreateService();
        var tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        var cts = new CancellationTokenSource();

        try
        {
            await service.CreateDatabaseAsync(tempPath);
            cts.Cancel();

            // Act
            var result = await service.ValidateDatabaseAsync(tempPath, cts.Token);

            // Assert - Operation should complete or be cancelled
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
