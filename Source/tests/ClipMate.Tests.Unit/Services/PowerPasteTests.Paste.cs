using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services.Abstractions;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

public partial class PowerPasteTests
{
    [Test]
    [Category("Paste")]
    public async Task Paste_WithDirectionDown_IncrementsPosition()
    {
        // Arrange
        var service = CreateService();
        var clips = new[] { CreateTestClip("First"), CreateTestClip("Second"), CreateTestClip("Third") };
        await service.StartAsync(clips, PowerPasteDirection.Down);

        // Act
        await PasteAsync();

        // Assert
        await Assert.That(service.CurrentPosition).IsEqualTo(1);
    }

    [Test]
    [Category("Paste")]
    public async Task Paste_WithDirectionUp_DecrementsPosition()
    {
        // Arrange
        var service = CreateService();
        var clips = new[] { CreateTestClip("First"), CreateTestClip("Second"), CreateTestClip("Third") };
        await service.StartAsync(clips, PowerPasteDirection.Up);

        // Act
        await PasteAsync();

        // Assert
        await Assert.That(service.CurrentPosition).IsEqualTo(1);
    }

    [Test]
    [Category("Paste")]
    public async Task Paste_WhenInactive_DoesNothing()
    {
        // Arrange
        var service = CreateService();

        // Act
        await PasteAsync();

        // Assert
        await Assert.That(service.State).IsEqualTo(PowerPasteState.Inactive);
        await Assert.That(service.CurrentPosition).IsEqualTo(-1);
    }

    [Test]
    [Category("Paste")]
    public async Task Paste_AtEndWithoutLoop_StopsService()
    {
        // Arrange
        var service = CreateService();
        var clips = new[] { CreateTestClip("First"), CreateTestClip("Second") };
        await service.StartAsync(clips, PowerPasteDirection.Down);
        await PasteAsync(); // Position 1

        // Act
        await PasteAsync(); // Position 2 (beyond end)

        // Assert
        await Assert.That(service.State).IsEqualTo(PowerPasteState.Inactive);
        A.CallTo(() => _mockSoundService.PlaySoundAsync(SoundEvent.PowerPasteComplete, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }

    [Test]
    [Category("Paste")]
    public async Task Paste_AtEndWithLoop_RestartsFromBeginning()
    {
        // Arrange
        var config = new ClipMateConfiguration
        {
            Preferences = new PreferencesConfiguration { PowerPasteLoop = true },
        };

        A.CallTo(() => _mockConfigService.Configuration).Returns(config);

        var service = CreateService();
        var clips = new[] { CreateTestClip("First"), CreateTestClip("Second") };
        await service.StartAsync(clips, PowerPasteDirection.Down);
        await PasteAsync(); // Position 1

        // Act
        await PasteAsync(); // Should loop to position 0

        // Assert
        await Assert.That(service.State).IsEqualTo(PowerPasteState.Active);
        await Assert.That(service.CurrentPosition).IsEqualTo(0);
    }

    [Test]
    [Category("Paste")]
    public async Task Paste_AtBeginningWithUpDirectionWithoutLoop_StopsService()
    {
        // Arrange
        var service = CreateService();
        var clips = new[] { CreateTestClip("First"), CreateTestClip("Second") };
        await service.StartAsync(clips, PowerPasteDirection.Up);
        await PasteAsync(); // Position 0

        // Act
        await PasteAsync(); // Position -1 (before beginning)

        // Assert
        await Assert.That(service.State).IsEqualTo(PowerPasteState.Inactive);
        A.CallTo(() => _mockSoundService.PlaySoundAsync(SoundEvent.PowerPasteComplete, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }

    [Test]
    [Category("Paste")]
    public async Task Paste_AtBeginningWithUpDirectionWithLoop_RestartsFromEnd()
    {
        // Arrange
        var config = new ClipMateConfiguration
        {
            Preferences = new PreferencesConfiguration { PowerPasteLoop = true },
        };

        A.CallTo(() => _mockConfigService.Configuration).Returns(config);

        var service = CreateService();
        var clips = new[] { CreateTestClip("First"), CreateTestClip("Second"), CreateTestClip("Third") };
        await service.StartAsync(clips, PowerPasteDirection.Up);
        await PasteAsync(); // Position 1
        await PasteAsync(); // Position 0

        // Act
        await PasteAsync(); // Should loop to position 2

        // Assert
        await Assert.That(service.State).IsEqualTo(PowerPasteState.Active);
        await Assert.That(service.CurrentPosition).IsEqualTo(2);
    }

    [Test]
    [Category("Paste")]
    public async Task Paste_FiresPositionChangedEvent()
    {
        // Arrange
        var service = CreateService();
        var clips = new[] { CreateTestClip("First"), CreateTestClip("Second") };
        await service.StartAsync(clips, PowerPasteDirection.Down);

        PowerPastePositionChangedEventArgs? eventArgs = null;
        service.PositionChanged += (_, e) => eventArgs = e;

        // Act
        await PasteAsync();

        // Assert
        await Assert.That(eventArgs).IsNotNull();
        await Assert.That(eventArgs!.Position).IsEqualTo(1);
        await Assert.That(eventArgs.TotalCount).IsEqualTo(2);
        await Assert.That(eventArgs.CurrentClip).IsNotNull();
    }
}
