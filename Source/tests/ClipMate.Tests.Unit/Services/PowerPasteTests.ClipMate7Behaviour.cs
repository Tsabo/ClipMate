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
public partial class PowerPasteTests
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
        await PasteAsync();

        // Act
        service.Stop();

        // Assert
        await Assert.That(CompleteSoundCount()).IsEqualTo(1);
    }

    [Test]
    [Category("CompleteSound")]
    public async Task Paste_WhenLooping_PlaysCompleteSoundTwice250MillisecondsApart()
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
        await PasteAsync();

        // Act
        await PasteAsync(); // starts over

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
    public async Task Paste_SingleClipWithoutExplodeAndLoopOn_StopsAfterOnePaste()
    {
        // Arrange
        UseConfiguration(new PreferencesConfiguration { PowerPasteLoop = true });
        var service = CreateService();
        await service.StartAsync([CreateTestClip("Only")], PowerPasteDirection.Down);

        // Act
        await PasteAsync();

        // Assert
        await Assert.That(service.State).IsEqualTo(PowerPasteState.Inactive);
        await Assert.That(CompleteSoundCount()).IsEqualTo(1);
    }

    [Test]
    [Category("SingleClip")]
    public async Task Paste_SingleExplodedClipWithLoopOn_StopsAfterItsLastFragment()
    {
        // Arrange - AdvanceNextItem 0x8669d8 ends a one-clip list before it looks at Loop, Explode included
        UseConfiguration(new PreferencesConfiguration { PowerPasteLoop = true, PowerPasteDelimiter = ",", PowerPasteTrim = true });
        var service = CreateService();
        UseExplode();
        await service.StartAsync([CreateTestClip("a,b")], PowerPasteDirection.Down);
        await PasteAsync();

        // Act
        await PasteAsync();

        // Assert
        await Assert.That(service.State).IsEqualTo(PowerPasteState.Inactive);
        await Assert.That(CompleteSoundCount()).IsEqualTo(1);
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
        UseExplode();
        await service.StartAsync([CreateTestClip("a,b")], PowerPasteDirection.Down);

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
    public async Task MoveTo_ClipOutsideSequence_StopsWithoutPuttingTheWaitingClipBack()
    {
        // Arrange - the clip list's pick handler 0x861f44 cancels: "Cancelled - Clip Outside of Original PowerPaste Range."
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second"), CreateTestClip("Third")], PowerPasteDirection.Down);
        await PasteAsync();

        // Act
        var moved = service.MoveTo(Guid.NewGuid());
        await Task.Yield();

        // Assert - the picked clip is copied by the clip list, as any pick is
        await Assert.That(moved).IsFalse();
        await Assert.That(service.State).IsEqualTo(PowerPasteState.Inactive);
        await Assert.That(_clipboard.Releases).IsEqualTo(1);
        A.CallTo(() => _mockCurrentClipWriter.PutOnClipboardAsync(A<Clip>.Ignored, A<WriteOptions>.Ignored, A<CancellationToken>.Ignored))
            .MustNotHaveHappened();
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
    public async Task MoveTo_ExplodedClip_GoesToItsFirstPieceEitherWay()
    {
        // Arrange
        UseConfiguration(new PreferencesConfiguration { PowerPasteDelimiter = ",", PowerPasteTrim = true });
        var target = CreateTestClip("c,d");
        var down = CreateService();
        UseExplode();
        await down.StartAsync([CreateTestClip("a,b"), target], PowerPasteDirection.Down);
        var up = CreateService();
        UseExplode();
        await up.StartAsync([target, CreateTestClip("a,b")], PowerPasteDirection.Up);

        // Act & Assert - both share the fake writer, so each is read straight after its move
        down.MoveTo(target.Id);
        await Assert.That(WaitingClip(down)?.TextContent).IsEqualTo("c");

        up.MoveTo(target.Id);
        await Assert.That(WaitingClip(up)?.TextContent).IsEqualTo("c");
    }

    [Test]
    [Category("ExplodeMode")]
    public async Task Up_WithExplode_PastesEachClipsPiecesInReadingOrder()
    {
        // Arrange: AdvanceNextItem 0x8669d8 walks fragment index +0x960 upward in either direction
        UseConfiguration(new PreferencesConfiguration { PowerPasteDelimiter = ",", PowerPasteTrim = true });
        var service = CreateService();
        UseExplode();
        await service.StartAsync([CreateTestClip("c,d"), CreateTestClip("a,b")], PowerPasteDirection.Up);
        var pasted = new List<string?> { WaitingClip(service)?.TextContent };

        // Act
        for (var i = 0; i < 3; i++)
        {
            await PasteAsync();
            pasted.Add(WaitingClip(service)?.TextContent);
        }

        // Assert
        await Assert.That(string.Join(",", pasted)).IsEqualTo("a,b,c,d");
    }

    [Test]
    [Category("LoadContent")]
    public async Task Start_WithALoader_LoadsOnlyTheClipThatComesUp()
    {
        // Arrange: a single selected clip runs to the end of the collection, which may hold thousands of clips
        var service = CreateService();
        var loaded = new List<Guid>();
        Clip[] clips = [CreateTestClip("First"), CreateTestClip("Second"), CreateTestClip("Third")];
        Task Load(Clip clip)
        {
            loaded.Add(clip.Id);
            return Task.CompletedTask;
        }

        // Act
        await service.StartAsync(clips, PowerPasteDirection.Down, Load);
        await WaitUntilOfferedAsync(1);

        // Assert
        await Assert.That(loaded).IsEquivalentTo(new[] { clips[0].Id });
        await Assert.That(WaitingClip(service)?.TextContent).IsEqualTo("First");
    }

    [Test]
    [Category("LoadContent")]
    public async Task Paste_WithALoaderAndExplode_ExplodesTheNextClipWhenItComesUp()
    {
        // Arrange
        UseConfiguration(new PreferencesConfiguration { PowerPasteDelimiter = ",", PowerPasteTrim = true });
        var service = CreateService();
        UseExplode();
        var second = CreateTestClip(string.Empty);
        await service.StartAsync([CreateTestClip("a"), second], PowerPasteDirection.Down, clip =>
        {
            if (clip == second)
                clip.TextContent = "b,c";

            return Task.CompletedTask;
        });
        await WaitUntilOfferedAsync(1);

        // Act
        await PasteAsync();
        await WaitUntilOfferedAsync(2);

        // Assert
        await Assert.That(WaitingClip(service)?.TextContent).IsEqualTo("b");
        await PasteAsync();
        await WaitUntilOfferedAsync(3);
        await Assert.That(WaitingClip(service)?.TextContent).IsEqualTo("c");
    }

    // A loader's content arrives after a task switch, so the offer it leads to can come a moment after the call
    private async Task WaitUntilOfferedAsync(int offers)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (_clipboard.Offers.Count < offers && DateTime.UtcNow < deadline)
            await Task.Yield();
    }
}
