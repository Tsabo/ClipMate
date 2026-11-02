using ClipMate.Data.Services;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Auto-Append's state: on with a growing clip, or off.
/// </summary>
public class AppendModeTests
{
    [Test]
    public async Task IsActive_Initially_IsFalse()
    {
        // Arrange
        var appendMode = new AppendMode();

        // Assert
        await Assert.That(appendMode.IsActive).IsFalse();
        await Assert.That(appendMode.GrowingClipId).IsNull();
    }

    [Test]
    public async Task Start_TurnsItOnWithTheGrowingClip()
    {
        // Arrange
        var appendMode = new AppendMode();
        var clipId = Guid.NewGuid();

        // Act
        appendMode.Start(clipId, "test-db");

        // Assert
        await Assert.That(appendMode.IsActive).IsTrue();
        await Assert.That(appendMode.GrowingClipId).IsEqualTo(clipId);
        await Assert.That(appendMode.DatabaseKey).IsEqualTo("test-db");
    }

    [Test]
    public async Task Stop_TurnsItOffAndForgetsTheGrowingClip()
    {
        // Arrange
        var appendMode = new AppendMode();
        appendMode.Start(Guid.NewGuid(), "test-db");

        // Act
        appendMode.Stop();

        // Assert
        await Assert.That(appendMode.IsActive).IsFalse();
        await Assert.That(appendMode.GrowingClipId).IsNull();
        await Assert.That(appendMode.DatabaseKey).IsNull();
    }
}
