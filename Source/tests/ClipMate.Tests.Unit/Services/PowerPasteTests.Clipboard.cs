using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services.Abstractions;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// How Power Paste uses the clipboard: what it offers, which reads move the sequence on, holding capture, and what is
/// left on the clipboard when it stops.
/// </summary>
public partial class PowerPasteTests
{
    [Test]
    [Category("Clipboard")]
    public async Task Start_HoldsCaptureAndOffersTheFirstClip()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);

        // Assert
        A.CallTo(() => _mockClipboardService.SuspendCapture()).MustHaveHappenedOnceExactly();
        await Assert.That(_clipboard.Offers).Count().IsEqualTo(1);
        await Assert.That(WaitingClip(service)?.TextContent).IsEqualTo("First");
    }

    [Test]
    [Category("Clipboard")]
    public async Task Start_AgainWhileActive_KeepsHoldingCapture()
    {
        // Arrange - the toggle's Down → Up starts again without giving up the clipboard
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);

        // Act
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Up);

        // Assert
        A.CallTo(() => _mockClipboardService.SuspendCapture()).MustHaveHappenedOnceExactly();
        await Assert.That(_clipboard.Releases).IsEqualTo(0);
        await Assert.That(WaitingClip(service)?.TextContent).IsEqualTo("Second");
    }

    [Test]
    [Category("Clipboard")]
    public async Task Start_WithExplodePiece_OffersPlainTextOnly()
    {
        // Arrange
        UseExplode();
        var service = CreateService();

        // Act
        await service.StartAsync([CreateTestClip("a\nb")], PowerPasteDirection.Down);

        // Assert - one Format, Unicode text, and the piece is reported as the Current Clip without being prepared
        await Assert.That(_clipboard.Offers[0]).Count().IsEqualTo(1);
        await Assert.That(_clipboard.Offers[0][0].FormatId).IsEqualTo(13u);
        A.CallTo(() => _mockCurrentClipWriter.ReportCurrent(A<Clip>.That.Matches(p => p.TextContent == "a"))).MustHaveHappenedOnceExactly();
        A.CallTo(() => _mockCurrentClipWriter.Prepare(A<Clip>.Ignored, A<WriteOptions>.Ignored)).MustNotHaveHappened();
    }

    [Test]
    [Category("Clipboard")]
    public async Task Start_WithActiveTemplate_OffersTheTemplatedTextAndKeepsTheClipUnchanged()
    {
        // Arrange
        var clip = CreateTestClip("Hello");
        A.CallTo(() => _mockTemplateService.PreviewTemplateText(clip)).Returns("Dear Hello, #1");
        var service = CreateService();

        // Act
        await service.StartAsync([clip], PowerPasteDirection.Down);

        // Assert
        await Assert.That(WaitingClip(service)?.TextContent).IsEqualTo("Dear Hello, #1");
        await Assert.That(clip.TextContent).IsEqualTo("Hello");
        A.CallTo(() => _mockTemplateService.AdvanceSequence()).MustNotHaveHappened();
    }

    [Test]
    [Category("Clipboard")]
    public async Task Paste_OfATemplatedClip_UsesUpOneSequenceNumber()
    {
        // Arrange
        A.CallTo(() => _mockTemplateService.PreviewTemplateText(A<Clip>.Ignored)).ReturnsLazily((Clip p) => p.TextContent + " #");
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);

        // Act
        await PasteAsync();

        // Assert
        A.CallTo(() => _mockTemplateService.AdvanceSequence()).MustHaveHappenedOnceExactly();
        await Assert.That(WaitingClip(service)?.TextContent).IsEqualTo("Second #");
    }

    [Test]
    [Category("Clipboard")]
    public async Task Paste_OffersTheNextClip()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);

        // Act
        await PasteAsync();

        // Assert
        await Assert.That(_clipboard.Offers).Count().IsEqualTo(2);
        await Assert.That(service.CurrentPosition).IsEqualTo(1);
        await Assert.That(WaitingClip(service)?.TextContent).IsEqualTo("Second");
    }

    [Test]
    [Category("Clipboard")]
    public async Task Paste_WithPowerPasteDelay_MovesOnOnlyAfterTheDelay()
    {
        // Arrange
        UseConfiguration(new PreferencesConfiguration { PowerPasteDelay = 200 });
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);

        // Act
        _clipboard.ReadBy(true);

        // Assert
        await Assert.That(service.CurrentPosition).IsEqualTo(0);
        _clock.Advance(TimeSpan.FromMilliseconds(199));
        await Assert.That(service.CurrentPosition).IsEqualTo(0);
        _clock.Advance(TimeSpan.FromMilliseconds(1));
        await Assert.That(service.CurrentPosition).IsEqualTo(1);
    }

    [Test]
    [Category("Clipboard")]
    public async Task Read_ByTheTargetTooSoonAfterAPaste_IsNotASecondPaste()
    {
        // Arrange - the target, or Windows, reading again what it was just given
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second"), CreateTestClip("Third")], PowerPasteDirection.Down);
        await PasteAsync();

        // Act
        _clock.Advance(TimeSpan.FromMilliseconds(499));
        _clipboard.ReadBy(true);

        // Assert
        await Assert.That(service.CurrentPosition).IsEqualTo(1);
        _clock.Advance(TimeSpan.FromMilliseconds(100));
        await Assert.That(_clipboard.OffersAgain).IsEqualTo(1);
    }

    [Test]
    [Category("Clipboard")]
    public async Task Read_ByAnotherProgram_OffersTheSameClipAgainAfterItSettles()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);

        // Act - clipboard history reads it, opening the clipboard twice for one change
        _clipboard.ReadBy(false);
        _clipboard.ReadBy(false);

        // Assert
        await Assert.That(_clipboard.OffersAgain).IsEqualTo(0);
        _clock.Advance(TimeSpan.FromMilliseconds(100));
        await Assert.That(_clipboard.OffersAgain).IsEqualTo(1);
        await Assert.That(service.CurrentPosition).IsEqualTo(0);
    }

    [Test]
    [Category("Clipboard")]
    public async Task Read_ByAReaderThatReadsEveryChange_IsThrottledToTheInterval()
    {
        // Arrange - a reader ignoring the "ignore me" markers reads each offer again
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);
        for (var i = 0; i < 3; i++)
        {
            _clipboard.ReadBy(false);
            _clock.Advance(TimeSpan.FromMilliseconds(100));
        }

        // Act
        _clipboard.ReadBy(false);

        // Assert - the fourth waits until a second after the first of the three, offered again at 100 ms
        _clock.Advance(TimeSpan.FromMilliseconds(799));
        await Assert.That(_clipboard.OffersAgain).IsEqualTo(3);
        _clock.Advance(TimeSpan.FromMilliseconds(1));
        await Assert.That(_clipboard.OffersAgain).IsEqualTo(4);
    }

    [Test]
    [Category("Clipboard")]
    public async Task Read_PendingWhenTheNextClipIsOffered_IsNotOfferedAgain()
    {
        // Arrange
        var service = CreateService();
        var second = CreateTestClip("Second");
        await service.StartAsync([CreateTestClip("First"), second], PowerPasteDirection.Down);
        _clipboard.ReadBy(false);

        // Act
        service.MoveTo(second.Id);
        _clock.Advance(TimeSpan.FromMilliseconds(100));

        // Assert
        await Assert.That(_clipboard.OffersAgain).IsEqualTo(0);
    }

    [Test]
    [Category("Clipboard")]
    public async Task Stop_ByTheUser_ReleasesTheClipboardResumesCaptureAndLeavesTheWaitingClip()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);

        // Act
        service.Stop();

        // Assert
        await Assert.That(_clipboard.Releases).IsEqualTo(1);
        A.CallTo(() => _mockClipboardService.ResumeCapture()).MustHaveHappenedOnceExactly();
        await WaitForPutBackAsync();
        A.CallTo(() => _mockCurrentClipWriter.PutOnClipboardAsync(A<Clip>.That.Matches(p => p.TextContent == "First"),
            A<WriteOptions>.Ignored, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }

    [Test]
    [Category("Clipboard")]
    public async Task Stop_ForAPickedClip_DoesNotPutTheWaitingClipBack()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);

        // Act
        service.Stop(false);
        await Task.Yield();

        // Assert
        await Assert.That(_clipboard.Releases).IsEqualTo(1);
        A.CallTo(() => _mockCurrentClipWriter.PutOnClipboardAsync(A<Clip>.Ignored, A<WriteOptions>.Ignored, A<CancellationToken>.Ignored))
            .MustNotHaveHappened();
    }

    [Test]
    [Category("Clipboard")]
    public async Task Lost_ToAnotherProgram_StopsWithoutPuttingTheWaitingClipBack()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);

        // Act
        _clipboard.TakeAway();
        await Task.Yield();

        // Assert
        await Assert.That(service.State).IsEqualTo(PowerPasteState.Inactive);
        A.CallTo(() => _mockClipboardService.ResumeCapture()).MustHaveHappenedOnceExactly();
        A.CallTo(() => _mockCurrentClipWriter.PutOnClipboardAsync(A<Clip>.Ignored, A<WriteOptions>.Ignored, A<CancellationToken>.Ignored))
            .MustNotHaveHappened();
    }

    [Test]
    [Category("Clipboard")]
    public async Task Paste_OfTheLastClip_LeavesItOnTheClipboard()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Last")], PowerPasteDirection.Down);
        await PasteAsync();

        // Act
        await PasteAsync();

        // Assert
        await Assert.That(service.State).IsEqualTo(PowerPasteState.Inactive);
        await WaitForPutBackAsync();
        A.CallTo(() => _mockCurrentClipWriter.PutOnClipboardAsync(A<Clip>.That.Matches(p => p.TextContent == "Last"),
            A<WriteOptions>.Ignored, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }

    [Test]
    [Category("Clipboard")]
    public async Task Read_AfterStop_IsIgnored()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);
        service.Stop(false);

        // Act
        _clipboard.ReadBy(false);
        _clock.Advance(TimeSpan.FromSeconds(1));

        // Assert
        await Assert.That(_clipboard.OffersAgain).IsEqualTo(0);
    }

    [Test]
    [Category("Toggle")]
    public async Task Toggle_CyclesOffDownUpOff()
    {
        // Arrange
        var service = CreateService();
        IReadOnlyList<Clip> clips = [CreateTestClip("First"), CreateTestClip("Second")];
        var loads = 0;
        Task<IReadOnlyList<Clip>> Load()
        {
            loads++;
            return Task.FromResult(clips);
        }

        // Act & Assert
        await service.ToggleAsync(Load);
        await Assert.That(service.State).IsEqualTo(PowerPasteState.Active);
        await Assert.That(service.Direction).IsEqualTo(PowerPasteDirection.Down);

        await service.ToggleAsync(Load);
        await Assert.That(service.Direction).IsEqualTo(PowerPasteDirection.Up);
        await Assert.That(WaitingClip(service)?.TextContent).IsEqualTo("Second");

        await service.ToggleAsync(Load);
        await Assert.That(service.State).IsEqualTo(PowerPasteState.Inactive);
        await Assert.That(loads).IsEqualTo(2);
    }

    [Test]
    [Category("Toggle")]
    public async Task Toggle_WithNothingSelected_LeavesPowerPasteOff()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.ToggleAsync(() => Task.FromResult<IReadOnlyList<Clip>>([]));

        // Assert
        await Assert.That(service.State).IsEqualTo(PowerPasteState.Inactive);
        await Assert.That(_clipboard.Offers).IsEmpty();
    }

    [Test]
    [Category("Toggle")]
    public async Task Start_SavesTheDirectionAsTheLastOneUsed()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.StartAsync([CreateTestClip("First")], PowerPasteDirection.Up);

        // Assert
        await Assert.That(_mockConfigService.Configuration.Preferences.PowerPasteLastDirection).IsEqualTo("Up");
    }

    /// <summary>
    /// The waiting clip is put back after the release, off the caller's stack.
    /// </summary>
    private async Task WaitForPutBackAsync()
    {
        var done = new TaskCompletionSource();
        A.CallTo(() => _mockCurrentClipWriter.PutOnClipboardAsync(A<Clip>.Ignored, A<WriteOptions>.Ignored, A<CancellationToken>.Ignored))
            .Invokes(() => done.TrySetResult())
            .Returns(Task.CompletedTask);

        if (Fake.GetCalls(_mockCurrentClipWriter).Any(p => p.Method.Name == nameof(ICurrentClipWriter.PutOnClipboardAsync)))
            return;

        await Task.WhenAny(done.Task, Task.Delay(TimeSpan.FromSeconds(5)));
    }
}
