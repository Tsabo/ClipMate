using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Tests.Unit.Networking;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// The sequence rules agreed with ClipMate 7: when "complete" sounds, Loop, a single clip, Explode pieces and moving
/// the sequence to a clip.
/// </summary>
public partial class PowerPasteServiceTests
{
    [Test]
    [Category("CompleteSound")]
    public async Task Stop_BeforeAnyPaste_DoesNotPlayCompleteSound()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);

        // Act
        service.Stop();

        // Assert
        await Assert.That(CompleteSoundCount()).IsEqualTo(0);
    }

    [Test]
    [Category("CompleteSound")]
    public async Task Stop_AfterAPaste_PlaysCompleteSoundOnce()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second"), CreateTestClip("Third")], PowerPasteDirection.Down);
        await service.AdvanceToNextAsync();

        // Act
        service.Stop();

        // Assert
        await Assert.That(CompleteSoundCount()).IsEqualTo(1);
    }

    [Test]
    [Category("CompleteSound")]
    public async Task AdvanceToNextAsync_WhenLooping_PlaysCompleteSoundTwice250MillisecondsApart()
    {
        // Arrange
        UseConfiguration(new PreferencesConfiguration { PowerPasteLoop = true });
        var clock = new ManualTimeProvider();
        var secondBeep = new TaskCompletionSource();
        var beeps = 0;
        A.CallTo(() => _mockSoundService.PlaySoundAsync(SoundEvent.PowerPasteComplete, A<CancellationToken>.Ignored))
            .Invokes(() =>
            {
                if (Interlocked.Increment(ref beeps) == 2)
                    secondBeep.TrySetResult();
            })
            .Returns(Task.CompletedTask);

        var service = CreateService(clock);
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);
        await service.AdvanceToNextAsync();

        // Act
        await service.AdvanceToNextAsync(); // starts over

        // Assert
        await Assert.That(beeps).IsEqualTo(1);
        clock.Advance(TimeSpan.FromMilliseconds(249));
        await Assert.That(beeps).IsEqualTo(1);

        clock.Advance(TimeSpan.FromMilliseconds(1));
        await Task.WhenAny(secondBeep.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        await Assert.That(beeps).IsEqualTo(2);
        await Assert.That(service.State).IsEqualTo(PowerPasteState.Active);
    }

    [Test]
    [Category("SingleClip")]
    public async Task AdvanceToNextAsync_SingleClipWithoutExplodeAndLoopOn_StopsAfterOnePaste()
    {
        // Arrange
        UseConfiguration(new PreferencesConfiguration { PowerPasteLoop = true });
        var service = CreateService();
        await service.StartAsync([CreateTestClip("Only")], PowerPasteDirection.Down);

        // Act
        await service.AdvanceToNextAsync();

        // Assert
        await Assert.That(service.State).IsEqualTo(PowerPasteState.Inactive);
        await Assert.That(CompleteSoundCount()).IsEqualTo(1);
    }

    [Test]
    [Category("SingleClip")]
    public async Task AdvanceToNextAsync_SingleExplodedClipWithLoopOn_StartsOver()
    {
        // Arrange
        UseConfiguration(new PreferencesConfiguration { PowerPasteLoop = true, PowerPasteDelimiter = ",", PowerPasteTrim = true });
        var service = CreateService();
        await service.StartAsync([CreateTestClip("a,b")], PowerPasteDirection.Down, true);
        await service.AdvanceToNextAsync();

        // Act
        await service.AdvanceToNextAsync();

        // Assert
        await Assert.That(service.State).IsEqualTo(PowerPasteState.Active);
        await Assert.That(service.GetCurrentClip()?.TextContent).IsEqualTo("a");
    }

    [Test]
    [Category("ExplodeMode")]
    public async Task StartAsync_WithExplode_MarksEachPieceAsExplodePiece()
    {
        // Arrange
        UseConfiguration(new PreferencesConfiguration { PowerPasteDelimiter = ",", PowerPasteTrim = true });
        var service = CreateService();
        PowerPastePositionChangedEventArgs? position = null;
        service.PositionChanged += (_, e) => position = e;

        // Act
        await service.StartAsync([CreateTestClip("a,b")], PowerPasteDirection.Down, true);

        // Assert
        await Assert.That(position?.IsExplodePiece).IsTrue();
        await Assert.That(position?.CurrentClip?.TextContent).IsEqualTo("a");
    }

    [Test]
    [Category("ExplodeMode")]
    public async Task StartAsync_WithoutExplode_DoesNotMarkClipAsExplodePiece()
    {
        // Arrange
        var service = CreateService();
        PowerPastePositionChangedEventArgs? position = null;
        service.PositionChanged += (_, e) => position = e;

        // Act
        await service.StartAsync([CreateTestClip("a,b")], PowerPasteDirection.Down);

        // Assert
        await Assert.That(position?.IsExplodePiece).IsFalse();
    }

    [Test]
    [Category("Stop")]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Stop_TellsWhetherToKeepTheWaitingClip(bool keepWaitingClip)
    {
        // Arrange - a stop by picking another clip, or by a copy elsewhere, must not put the waiting clip back
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);
        PowerPasteStateChangedEventArgs? stopped = null;
        service.StateChanged += (_, e) => stopped = e;

        // Act
        service.Stop(keepWaitingClip);

        // Assert
        await Assert.That(stopped?.KeepWaitingClip).IsEqualTo(keepWaitingClip);
    }

    [Test]
    [Category("Stop")]
    public async Task AdvanceToNextAsync_AtTheEnd_KeepsTheLastClip()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);
        await service.AdvanceToNextAsync();
        PowerPasteStateChangedEventArgs? stopped = null;
        service.StateChanged += (_, e) => stopped = e;

        // Act
        await service.AdvanceToNextAsync();

        // Assert
        await Assert.That(stopped?.KeepWaitingClip).IsTrue();
    }

    [Test]
    [Category("Status")]
    public async Task Status_FollowsStartAndStop()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        await service.StartAsync([CreateTestClip("First")], PowerPasteDirection.Down);
        await Assert.That(_status.IsActive).IsTrue();

        service.Stop();
        await Assert.That(_status.IsActive).IsFalse();
    }

    [Test]
    [Category("MoveTo")]
    public async Task MoveTo_ClipInSequence_MakesItTheWaitingClip()
    {
        // Arrange
        var second = CreateTestClip("Second");
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), second, CreateTestClip("Third")], PowerPasteDirection.Down);
        PowerPastePositionChangedEventArgs? position = null;
        service.PositionChanged += (_, e) => position = e;

        // Act
        var moved = service.MoveTo(second.Id);

        // Assert
        await Assert.That(moved).IsTrue();
        await Assert.That(service.CurrentPosition).IsEqualTo(1);
        await Assert.That(position?.CurrentClip).IsEqualTo(second);
    }

    [Test]
    [Category("MoveTo")]
    public async Task MoveTo_ClipOutsideSequence_ReturnsFalseAndKeepsPosition()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);

        // Act
        var moved = service.MoveTo(Guid.NewGuid());

        // Assert
        await Assert.That(moved).IsFalse();
        await Assert.That(service.CurrentPosition).IsEqualTo(0);
        await Assert.That(service.State).IsEqualTo(PowerPasteState.Active);
    }

    [Test]
    [Category("MoveTo")]
    public async Task MoveTo_WhenInactive_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();

        // Act
        var moved = service.MoveTo(Guid.NewGuid());

        // Assert
        await Assert.That(moved).IsFalse();
    }

    [Test]
    [Category("MoveTo")]
    public async Task MoveTo_ExplodedClip_GoesToItsFirstPieceDownAndLastPieceUp()
    {
        // Arrange
        UseConfiguration(new PreferencesConfiguration { PowerPasteDelimiter = ",", PowerPasteTrim = true });
        var target = CreateTestClip("c,d");
        var down = CreateService();
        await down.StartAsync([CreateTestClip("a,b"), target], PowerPasteDirection.Down, true);
        var up = CreateService();
        await up.StartAsync([target, CreateTestClip("a,b")], PowerPasteDirection.Up, true);

        // Act
        down.MoveTo(target.Id);
        up.MoveTo(target.Id);

        // Assert
        await Assert.That(down.GetCurrentClip()?.TextContent).IsEqualTo("c");
        await Assert.That(up.GetCurrentClip()?.TextContent).IsEqualTo("d");
    }
}
