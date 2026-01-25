using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using Moq;

namespace ClipMate.Tests.Unit.Services;

public partial class PowerPasteServiceTests
{
    [Test]
    [Category("AdvanceToNextAsync")]
    public async Task AdvanceToNextAsync_WithDirectionDown_IncrementsPosition()
    {
        // Arrange
        var service = CreateService();
        var clips = new[] { CreateTestClip("First"), CreateTestClip("Second"), CreateTestClip("Third") };
        await service.StartAsync(clips, PowerPasteDirection.Down);

        // Act
        await service.AdvanceToNextAsync();

        // Assert
        await Assert.That(service.CurrentPosition).IsEqualTo(1);
    }

    [Test]
    [Category("AdvanceToNextAsync")]
    public async Task AdvanceToNextAsync_WithDirectionUp_DecrementsPosition()
    {
        // Arrange
        var service = CreateService();
        var clips = new[] { CreateTestClip("First"), CreateTestClip("Second"), CreateTestClip("Third") };
        await service.StartAsync(clips, PowerPasteDirection.Up);

        // Act
        await service.AdvanceToNextAsync();

        // Assert
        await Assert.That(service.CurrentPosition).IsEqualTo(1);
    }

    [Test]
    [Category("AdvanceToNextAsync")]
    public async Task AdvanceToNextAsync_WhenInactive_DoesNothing()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.AdvanceToNextAsync();

        // Assert
        await Assert.That(service.State).IsEqualTo(PowerPasteState.Inactive);
        await Assert.That(service.CurrentPosition).IsEqualTo(-1);
    }

    [Test]
    [Category("AdvanceToNextAsync")]
    public async Task AdvanceToNextAsync_AtEndWithoutLoop_StopsService()
    {
        // Arrange
        var service = CreateService();
        var clips = new[] { CreateTestClip("First"), CreateTestClip("Second") };
        await service.StartAsync(clips, PowerPasteDirection.Down);
        await service.AdvanceToNextAsync(); // Position 1

        // Act
        await service.AdvanceToNextAsync(); // Position 2 (beyond end)

        // Assert
        await Assert.That(service.State).IsEqualTo(PowerPasteState.Inactive);
        _mockSoundService.Verify(p => p.PlaySoundAsync(SoundEvent.PowerPasteComplete, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("AdvanceToNextAsync")]
    public async Task AdvanceToNextAsync_AtEndWithLoop_RestartsFromBeginning()
    {
        // Arrange
        var config = new ClipMateConfiguration
        {
            Preferences = new PreferencesConfiguration { PowerPasteLoop = true },
        };

        _mockConfigService.Setup(p => p.Configuration).Returns(config);

        var service = CreateService();
        var clips = new[] { CreateTestClip("First"), CreateTestClip("Second") };
        await service.StartAsync(clips, PowerPasteDirection.Down);
        await service.AdvanceToNextAsync(); // Position 1

        // Act
        await service.AdvanceToNextAsync(); // Should loop to position 0

        // Assert
        await Assert.That(service.State).IsEqualTo(PowerPasteState.Active);
        await Assert.That(service.CurrentPosition).IsEqualTo(0);
        _mockSoundService.Verify(p => p.PlaySoundAsync(SoundEvent.PowerPasteComplete, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Test]
    [Category("AdvanceToNextAsync")]
    public async Task AdvanceToNextAsync_AtBeginningWithUpDirectionWithoutLoop_StopsService()
    {
        // Arrange
        var service = CreateService();
        var clips = new[] { CreateTestClip("First"), CreateTestClip("Second") };
        await service.StartAsync(clips, PowerPasteDirection.Up);
        await service.AdvanceToNextAsync(); // Position 0

        // Act
        await service.AdvanceToNextAsync(); // Position -1 (before beginning)

        // Assert
        await Assert.That(service.State).IsEqualTo(PowerPasteState.Inactive);
        _mockSoundService.Verify(p => p.PlaySoundAsync(SoundEvent.PowerPasteComplete, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("AdvanceToNextAsync")]
    public async Task AdvanceToNextAsync_AtBeginningWithUpDirectionWithLoop_RestartsFromEnd()
    {
        // Arrange
        var config = new ClipMateConfiguration
        {
            Preferences = new PreferencesConfiguration { PowerPasteLoop = true },
        };

        _mockConfigService.Setup(p => p.Configuration).Returns(config);

        var service = CreateService();
        var clips = new[] { CreateTestClip("First"), CreateTestClip("Second"), CreateTestClip("Third") };
        await service.StartAsync(clips, PowerPasteDirection.Up);
        await service.AdvanceToNextAsync(); // Position 1
        await service.AdvanceToNextAsync(); // Position 0

        // Act
        await service.AdvanceToNextAsync(); // Should loop to position 2

        // Assert
        await Assert.That(service.State).IsEqualTo(PowerPasteState.Active);
        await Assert.That(service.CurrentPosition).IsEqualTo(2);
    }

    [Test]
    [Category("AdvanceToNextAsync")]
    public async Task AdvanceToNextAsync_FiresPositionChangedEvent()
    {
        // Arrange
        var service = CreateService();
        var clips = new[] { CreateTestClip("First"), CreateTestClip("Second") };
        await service.StartAsync(clips, PowerPasteDirection.Down);

        PowerPastePositionChangedEventArgs? eventArgs = null;
        service.PositionChanged += (_, e) => eventArgs = e;

        // Act
        await service.AdvanceToNextAsync();

        // Assert
        await Assert.That(eventArgs).IsNotNull();
        await Assert.That(eventArgs!.Position).IsEqualTo(1);
        await Assert.That(eventArgs.TotalCount).IsEqualTo(2);
        await Assert.That(eventArgs.CurrentClip).IsNotNull();
    }
}
