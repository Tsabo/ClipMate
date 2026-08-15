using ClipMate.Data.Services;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Unit tests for AutoAppendService.
/// Tests the in-memory toggle state used by Auto-Append mode.
/// </summary>
public class AutoAppendServiceTests : TestFixtureBase
{
    [Test]
    public async Task IsActive_Initially_ReturnsFalse()
    {
        // Arrange
        var service = new AutoAppendService();

        // Act & Assert
        await Assert.That(service.IsActive).IsFalse();
        await Assert.That(service.GrowingClipId).IsNull();
        await Assert.That(service.DatabaseKey).IsNull();
    }

    [Test]
    public async Task Activate_SetsIsActiveTrue()
    {
        // Arrange
        var service = new AutoAppendService();

        // Act
        service.Activate();

        // Assert
        await Assert.That(service.IsActive).IsTrue();
        await Assert.That(service.GrowingClipId).IsNull();
    }

    [Test]
    public async Task Deactivate_ClearsActiveStateAndGrowingClip()
    {
        // Arrange
        var service = new AutoAppendService();
        service.Activate();
        service.SetGrowingClip(Guid.NewGuid(), "test-db");

        // Act
        service.Deactivate();

        // Assert
        await Assert.That(service.IsActive).IsFalse();
        await Assert.That(service.GrowingClipId).IsNull();
        await Assert.That(service.DatabaseKey).IsNull();
    }

    [Test]
    public async Task SetGrowingClip_StoresClipIdAndDatabaseKey()
    {
        // Arrange
        var service = new AutoAppendService();
        service.Activate();
        var clipId = Guid.NewGuid();

        // Act
        service.SetGrowingClip(clipId, "test-db");

        // Assert
        await Assert.That(service.GrowingClipId).IsEqualTo(clipId);
        await Assert.That(service.DatabaseKey).IsEqualTo("test-db");
    }

    [Test]
    public async Task Activate_WhenAlreadyGrowing_ResetsGrowingClip()
    {
        // Arrange
        var service = new AutoAppendService();
        service.Activate();
        service.SetGrowingClip(Guid.NewGuid(), "test-db");

        // Act
        service.Activate();

        // Assert
        await Assert.That(service.IsActive).IsTrue();
        await Assert.That(service.GrowingClipId).IsNull();
        await Assert.That(service.DatabaseKey).IsNull();
    }
}
