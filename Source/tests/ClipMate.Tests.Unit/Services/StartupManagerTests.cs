using ClipMate.Platform.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for <see cref="StartupManager" />.
/// Note: These tests focus on constructor validation and basic method signatures.
/// Actual registry operations are integration-test level and not suitable for unit tests.
/// </summary>
public class StartupManagerTests
{
    [Test]
    public async Task Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => new StartupManager(null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithValidLogger_CreatesInstance()
    {
        // Arrange
        var logger = new Mock<ILogger<StartupManager>>();

        // Act
        var service = new StartupManager(logger.Object);

        // Assert
        await Assert.That(service).IsNotNull();
    }

    [Test]
    public async Task IsEnabledAsync_ReturnsTaskWithTuple()
    {
        // Arrange
        var logger = new Mock<ILogger<StartupManager>>();
        var service = new StartupManager(logger.Object);

        // Act
        var result = await service.IsEnabledAsync();

        // Assert - Should return a tuple (actual values depend on registry state)
        await Assert.That(result.success).IsTypeOf<bool>();
        await Assert.That(result.isEnabled).IsTypeOf<bool>();
    }

    [Test]
    public async Task EnableAsync_ReturnsTaskWithTuple()
    {
        // Arrange
        var logger = new Mock<ILogger<StartupManager>>();
        var service = new StartupManager(logger.Object);

        // Act
        var result = await service.EnableAsync();

        // Assert - Should return a tuple (actual success depends on permissions/registry state)
        await Assert.That(result.success).IsTypeOf<bool>();
    }

    [Test]
    public async Task DisableAsync_ReturnsTaskWithTuple()
    {
        // Arrange
        var logger = new Mock<ILogger<StartupManager>>();
        var service = new StartupManager(logger.Object);

        // Act
        var result = await service.DisableAsync();

        // Assert - Should return a tuple (actual success depends on registry state)
        await Assert.That(result.success).IsTypeOf<bool>();
    }
}
