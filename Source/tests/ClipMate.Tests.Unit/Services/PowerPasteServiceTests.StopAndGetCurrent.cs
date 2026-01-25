using ClipMate.Core.Services;

namespace ClipMate.Tests.Unit.Services;

public partial class PowerPasteServiceTests
{
    [Test]
    [Category("Stop")]
    public async Task Stop_WhenActive_DeactivatesService()
    {
        // Arrange
        var service = CreateService();
        var clips = new[] { CreateTestClip("Test") };
        await service.StartAsync(clips, PowerPasteDirection.Down);

        // Act
        service.Stop();

        // Assert
        await Assert.That(service.State).IsEqualTo(PowerPasteState.Inactive);
        await Assert.That(service.CurrentPosition).IsEqualTo(-1);
        await Assert.That(service.TotalCount).IsEqualTo(0);
    }

    [Test]
    [Category("Stop")]
    public async Task Stop_WhenInactive_DoesNothing()
    {
        // Arrange
        var service = CreateService();

        // Act
        service.Stop();

        // Assert
        await Assert.That(service.State).IsEqualTo(PowerPasteState.Inactive);
    }

    [Test]
    [Category("Stop")]
    public async Task Stop_FiresStateChangedEvent()
    {
        // Arrange
        var service = CreateService();
        var clips = new[] { CreateTestClip("Test") };
        await service.StartAsync(clips, PowerPasteDirection.Down);

        PowerPasteStateChangedEventArgs? eventArgs = null;
        service.StateChanged += (_, e) => eventArgs = e;

        // Act
        service.Stop();

        // Assert
        await Assert.That(eventArgs).IsNotNull();
        await Assert.That(eventArgs!.OldState).IsEqualTo(PowerPasteState.Active);
        await Assert.That(eventArgs.NewState).IsEqualTo(PowerPasteState.Inactive);
        await Assert.That(eventArgs.TotalCount).IsEqualTo(0);
    }

    [Test]
    [Category("GetCurrentClip")]
    public async Task GetCurrentClip_WhenActive_ReturnsClipAtPosition()
    {
        // Arrange
        var service = CreateService();
        var clips = new[] { CreateTestClip("First"), CreateTestClip("Second"), CreateTestClip("Third") };
        await service.StartAsync(clips, PowerPasteDirection.Down);

        // Act
        var current = service.GetCurrentClip();

        // Assert
        await Assert.That(current).IsNotNull();
        await Assert.That(current!.TextContent).IsEqualTo("First");
    }

    [Test]
    [Category("GetCurrentClip")]
    public async Task GetCurrentClip_WhenInactive_ReturnsNull()
    {
        // Arrange
        var service = CreateService();

        // Act
        var current = service.GetCurrentClip();

        // Assert
        await Assert.That(current).IsNull();
    }

    [Test]
    [Category("GetCurrentClip")]
    public async Task GetCurrentClip_AfterAdvancing_ReturnsNextClip()
    {
        // Arrange
        var service = CreateService();
        var clips = new[] { CreateTestClip("First"), CreateTestClip("Second"), CreateTestClip("Third") };
        await service.StartAsync(clips, PowerPasteDirection.Down);
        await service.AdvanceToNextAsync();

        // Act
        var current = service.GetCurrentClip();

        // Assert
        await Assert.That(current).IsNotNull();
        await Assert.That(current!.TextContent).IsEqualTo("Second");
    }
}
