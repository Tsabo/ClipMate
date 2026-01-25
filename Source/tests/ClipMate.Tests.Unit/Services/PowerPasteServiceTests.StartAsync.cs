using ClipMate.Core.Services;

namespace ClipMate.Tests.Unit.Services;

public partial class PowerPasteServiceTests
{
    [Test]
    [Category("StartAsync")]
    public async Task StartAsync_WithValidClips_ActivatesService()
    {
        // Arrange
        var service = CreateService();
        var clips = new[] { CreateTestClip("Clip 1"), CreateTestClip("Clip 2"), CreateTestClip("Clip 3") };
        var stateChangedFired = false;
        var positionChangedFired = false;

        service.StateChanged += (_, _) => stateChangedFired = true;
        service.PositionChanged += (_, _) => positionChangedFired = true;

        // Act
        await service.StartAsync(clips, PowerPasteDirection.Down);

        // Assert
        await Assert.That(service.State).IsEqualTo(PowerPasteState.Active);
        await Assert.That(service.Direction).IsEqualTo(PowerPasteDirection.Down);
        await Assert.That(service.CurrentPosition).IsEqualTo(0);
        await Assert.That(service.TotalCount).IsEqualTo(3);
        await Assert.That(stateChangedFired).IsTrue();
        await Assert.That(positionChangedFired).IsTrue();
    }

    [Test]
    [Category("StartAsync")]
    public async Task StartAsync_WithDirectionDown_StartsAtBeginning()
    {
        // Arrange
        var service = CreateService();
        var clips = new[] { CreateTestClip("First"), CreateTestClip("Second") };

        // Act
        await service.StartAsync(clips, PowerPasteDirection.Down);

        // Assert
        await Assert.That(service.CurrentPosition).IsEqualTo(0);
        await Assert.That(service.Direction).IsEqualTo(PowerPasteDirection.Down);
    }

    [Test]
    [Category("StartAsync")]
    public async Task StartAsync_WithDirectionUp_StartsAtEnd()
    {
        // Arrange
        var service = CreateService();
        var clips = new[] { CreateTestClip("First"), CreateTestClip("Second"), CreateTestClip("Third") };

        // Act
        await service.StartAsync(clips, PowerPasteDirection.Up);

        // Assert
        await Assert.That(service.CurrentPosition).IsEqualTo(2);
        await Assert.That(service.Direction).IsEqualTo(PowerPasteDirection.Up);
    }

    [Test]
    [Category("StartAsync")]
    public async Task StartAsync_WithNullClips_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await service.StartAsync(null!, PowerPasteDirection.Down));
    }

    [Test]
    [Category("StartAsync")]
    public async Task StartAsync_WithEmptyClips_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await service.StartAsync([], PowerPasteDirection.Down));
    }

    [Test]
    [Category("StartAsync")]
    public async Task StartAsync_FiresStateChangedEvent()
    {
        // Arrange
        var service = CreateService();
        var clips = new[] { CreateTestClip("Test") };
        PowerPasteStateChangedEventArgs? eventArgs = null;

        service.StateChanged += (_, e) => eventArgs = e;

        // Act
        await service.StartAsync(clips, PowerPasteDirection.Down);

        // Assert
        await Assert.That(eventArgs).IsNotNull();
        await Assert.That(eventArgs!.OldState).IsEqualTo(PowerPasteState.Inactive);
        await Assert.That(eventArgs.NewState).IsEqualTo(PowerPasteState.Active);
        await Assert.That(eventArgs.Direction).IsEqualTo(PowerPasteDirection.Down);
        await Assert.That(eventArgs.TotalCount).IsEqualTo(1);
    }

    [Test]
    [Category("StartAsync")]
    public async Task StartAsync_FiresPositionChangedEvent()
    {
        // Arrange
        var service = CreateService();
        var clips = new[] { CreateTestClip("Test") };
        PowerPastePositionChangedEventArgs? eventArgs = null;

        service.PositionChanged += (_, e) => eventArgs = e;

        // Act
        await service.StartAsync(clips, PowerPasteDirection.Down);

        // Assert
        await Assert.That(eventArgs).IsNotNull();
        await Assert.That(eventArgs!.Position).IsEqualTo(0);
        await Assert.That(eventArgs.TotalCount).IsEqualTo(1);
        await Assert.That(eventArgs.CurrentClip).IsNotNull();
        await Assert.That(eventArgs.IsComplete).IsFalse();
    }

    [Test]
    [Category("StartAsync")]
    public async Task StartAsync_WithExplodeMode_SplitsClip()
    {
        // Arrange
        var service = CreateService();
        var clip = CreateTestClip("Line1\nLine2\nLine3");

        // Act
        await service.StartAsync([clip], PowerPasteDirection.Down, true);

        // Assert
        await Assert.That(service.TotalCount).IsEqualTo(3);
    }
}
