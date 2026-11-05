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
        _clock.Advance(TimeSpan.FromMilliseconds(249));
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
        // Arrange - a reader ignoring the "ignore me" markers reads each offer again. It has no name, so it may be a paste.
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);
        for (var i = 0; i < 20; i++)
        {
            _clipboard.ReadWithoutAProcess(TimeSpan.FromHours(1));
            _clock.Advance(TimeSpan.FromMilliseconds(25));
        }

        // Act
        _clipboard.ReadWithoutAProcess(TimeSpan.FromHours(1));

        // Assert - the 21st waits until a second after the first of the twenty, offered again at 25 ms
        _clock.Advance(TimeSpan.FromMilliseconds(524));
        await Assert.That(_clipboard.OffersAgain).IsEqualTo(20);
        _clock.Advance(TimeSpan.FromMilliseconds(1));
        await Assert.That(_clipboard.OffersAgain).IsEqualTo(21);
    }

    [Test]
    [Category("Clipboard")]
    public async Task Read_ByAProgramThatKeepsReading_IsRefusedAndNotOfferedAgain()
    {
        // Arrange - Remote Desktop's client reading every offer, 15 times a second
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);
        var served = 0;
        while (_clipboard.ReadInTheBackground())
        {
            served++;
            _clock.Advance(TimeSpan.FromMilliseconds(66));
        }

        var offersAgain = _clipboard.OffersAgain;

        // Act
        _clock.Advance(TimeSpan.FromSeconds(5));
        var servedLater = _clipboard.ReadInTheBackground();
        _clock.Advance(TimeSpan.FromSeconds(5));

        // Assert - the clipboard stops changing, so the program has no reason to read and nothing else wakes up
        await Assert.That(served).IsEqualTo(9);
        await Assert.That(servedLater).IsFalse();
        await Assert.That(_clipboard.OffersAgain).IsEqualTo(offersAgain);
    }

    [Test]
    [Category("Clipboard")]
    public async Task Read_ByAProgramThatKeepsReading_DoesNotHideTheNextPaste()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);
        while (_clipboard.ReadInTheBackground())
            _clock.Advance(TimeSpan.FromMilliseconds(66));

        // Act
        await PasteAsync();

        // Assert
        await Assert.That(service.CurrentPosition).IsEqualTo(1);
    }

    [Test]
    [Category("Clipboard")]
    public async Task Read_ByAProgramReadingNowAndThen_IsAlwaysGivenTheData()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);
        var served = 0;

        // Act - a burst of three for each of many changes, a second apart
        for (var i = 0; i < 12; i++)
        {
            for (var j = 0; j < 3; j++)
            {
                if (_clipboard.ReadInTheBackground())
                    served++;
            }

            _clock.Advance(TimeSpan.FromSeconds(1));
        }

        // Assert
        await Assert.That(served).IsEqualTo(36);
    }

    [Test]
    [Category("Clipboard")]
    public async Task Read_ByARefusedProgram_IsServedAgainWhenPowerPasteNextStarts()
    {
        // Arrange
        var service = CreateService();
        var clips = new[] { CreateTestClip("First"), CreateTestClip("Second") };
        await service.StartAsync(clips, PowerPasteDirection.Down);
        while (_clipboard.ReadInTheBackground())
            _clock.Advance(TimeSpan.FromMilliseconds(66));

        // Act
        service.Stop();
        await service.StartAsync(clips, PowerPasteDirection.Down);

        // Assert
        await Assert.That(_clipboard.ReadInTheBackground()).IsTrue();
    }

    [Test]
    [Category("Clipboard")]
    public async Task Read_ByAnotherProgram_WhenOneIsRefused_IsGivenTheData()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);
        while (_clipboard.ReadInTheBackground())
            _clock.Advance(TimeSpan.FromMilliseconds(66));

        // Act and assert
        await Assert.That(_clipboard.ReadInTheBackground("clipboard history")).IsTrue();
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
    public async Task Toggle_FirstUse_StartsGoingUp()
    {
        // Arrange: act_PPExecute 0x866228 starts Up unless the direction field 0x931 holds Down, and it starts at 0
        var service = CreateService();

        // Act
        await service.ToggleAsync(_ => [CreateTestClip("First"), CreateTestClip("Second")]);

        // Assert
        await Assert.That(service.State).IsEqualTo(PowerPasteState.Active);
        await Assert.That(service.Direction).IsEqualTo(PowerPasteDirection.Up);
        await Assert.That(WaitingClip(service)?.TextContent).IsEqualTo("Second");
    }

    [Test]
    [Category("Toggle")]
    public async Task Toggle_StoppedWithoutAPaste_GoesTheOtherWayNextTime()
    {
        // Arrange: PPStop 0x866ecc flips the direction when the paste count is 0
        var service = CreateService();
        IReadOnlyList<Clip> clips = [CreateTestClip("First"), CreateTestClip("Second")];
        await service.ToggleAsync(_ => clips);

        // Act
        await service.ToggleAsync(_ => clips);
        await Assert.That(service.State).IsEqualTo(PowerPasteState.Inactive);
        await service.ToggleAsync(_ => clips);

        // Assert
        await Assert.That(service.Direction).IsEqualTo(PowerPasteDirection.Down);
        await Assert.That(WaitingClip(service)?.TextContent).IsEqualTo("First");
    }

    [Test]
    [Category("Toggle")]
    public async Task Toggle_StoppedAfterAPaste_KeepsTheDirection()
    {
        // Arrange
        var service = CreateService();
        IReadOnlyList<Clip> clips = [CreateTestClip("First"), CreateTestClip("Second"), CreateTestClip("Third")];
        await service.ToggleAsync(_ => clips);
        await PasteAsync();

        // Act
        await service.ToggleAsync(_ => clips);
        await service.ToggleAsync(_ => clips);

        // Assert
        await Assert.That(service.Direction).IsEqualTo(PowerPasteDirection.Up);
    }

    [Test]
    [Category("Toggle")]
    public async Task ToggleDirection_AlreadyGoingThatWay_Stops()
    {
        // Arrange
        var service = CreateService();
        IReadOnlyList<Clip> clips = [CreateTestClip("First"), CreateTestClip("Second")];
        await service.ToggleAsync(PowerPasteDirection.Down, _ => clips);

        // Act
        await service.ToggleAsync(PowerPasteDirection.Down, _ => clips);

        // Assert
        await Assert.That(service.State).IsEqualTo(PowerPasteState.Inactive);
    }

    [Test]
    [Category("Toggle")]
    public async Task ToggleDirection_GoingTheOtherWay_StartsThatWay()
    {
        // Arrange
        var service = CreateService();
        IReadOnlyList<Clip> clips = [CreateTestClip("First"), CreateTestClip("Second")];
        await service.ToggleAsync(PowerPasteDirection.Down, _ => clips);

        // Act
        await service.ToggleAsync(PowerPasteDirection.Up, _ => clips);

        // Assert
        await Assert.That(service.State).IsEqualTo(PowerPasteState.Active);
        await Assert.That(service.Direction).IsEqualTo(PowerPasteDirection.Up);
        await Assert.That(WaitingClip(service)?.TextContent).IsEqualTo("Second");
    }

    [Test]
    [Category("PasteDetection")]
    public async Task Read_WithoutAProcessJustAfterTheUsersInput_IsAPaste()
    {
        // Arrange: a browser or Windows Terminal reads without a window, and a Store app's read comes from a Windows service
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);
        _clock.Advance(TimeSpan.FromSeconds(1));

        // Act
        _clipboard.ReadWithoutAProcess(TimeSpan.FromMilliseconds(20));

        // Assert
        await Assert.That(WaitingClip(service)?.TextContent).IsEqualTo("Second");
    }

    [Test]
    [Category("PasteDetection")]
    public async Task Read_WithoutAProcessWithNoRecentInput_IsNotAPaste()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);
        _clock.Advance(TimeSpan.FromSeconds(1));

        // Act
        _clipboard.ReadWithoutAProcess(TimeSpan.FromSeconds(5));

        // Assert
        await Assert.That(WaitingClip(service)?.TextContent).IsEqualTo("First");
    }

    [Test]
    [Category("PasteDetection")]
    public async Task Read_WithoutAProcess_WhenTheLastInputCameBeforeTheClip_IsNotAPaste()
    {
        // Arrange: the input is the key press that pasted the clip before this one, released as this one came up
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);
        _clock.Advance(TimeSpan.FromMilliseconds(420));

        // Act: the input was 300 ms ago, so 120 ms after the offer
        _clipboard.ReadWithoutAProcess(TimeSpan.FromMilliseconds(300));

        // Assert
        await Assert.That(WaitingClip(service)?.TextContent).IsEqualTo("First");
    }

    [Test]
    [Category("PasteDetection")]
    public async Task Read_ByABackgroundProgramWhileTheUserTypes_IsNotAPaste()
    {
        // Arrange: WSLg's msrdc reads every clipboard change; Tab between two pastes must not count as pasting
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);
        _clock.Advance(TimeSpan.FromSeconds(1));

        // Act
        _clipboard.ReadByABackgroundProgramWhileTheUserTypes();

        // Assert
        await Assert.That(WaitingClip(service)?.TextContent).IsEqualTo("First");
    }

    [Test]
    [Category("PasteDetection")]
    public async Task Read_ByClipMateOrWithItInTheForeground_IsNotAPaste()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);
        _clock.Advance(TimeSpan.FromSeconds(1));

        // Act
        _clipboard.ReadByClipMate();
        _clipboard.ReadWithClipMateInTheForeground();

        // Assert
        await Assert.That(WaitingClip(service)?.TextContent).IsEqualTo("First");
    }

    [Test]
    [Category("Clipboard")]
    public async Task Read_ByTheTarget300MillisecondsAfterAPaste_IsAnotherPaste()
    {
        // Arrange - the manual says pasting "very rapidly"
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second"), CreateTestClip("Third")], PowerPasteDirection.Down);
        await PasteAsync();

        // Act
        _clock.Advance(TimeSpan.FromMilliseconds(300));
        _clipboard.ReadBy(true);

        // Assert
        await Assert.That(WaitingClip(service)?.TextContent).IsEqualTo("Third");
    }

    [Test]
    [Category("Shield")]
    public async Task ShieldOff_TheTargetReadingAgainAtOnce_IsAnotherPaste()
    {
        // Arrange - ClipMate 7 has no limit on how soon the next read counts
        UseConfiguration(new PreferencesConfiguration { PowerPasteShield = false, PowerPasteDelay = 0 });
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second"), CreateTestClip("Third")], PowerPasteDirection.Down);
        await PasteAsync();

        // Act
        _clipboard.ReadBy(true);

        // Assert
        await Assert.That(WaitingClip(service)?.TextContent).IsEqualTo("Third");
    }

    [Test]
    [Category("Shield")]
    public async Task ShieldOff_ReadByABackgroundProgram_IsAPaste()
    {
        // Arrange: ClipMate 7's ClipViewRenderFormat 0x8666e4 does not look at who reads
        UseConfiguration(new PreferencesConfiguration { PowerPasteShield = false, PowerPasteDelay = 0 });
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);

        // Act
        _clipboard.ReadBy(false);

        // Assert
        await Assert.That(WaitingClip(service)?.TextContent).IsEqualTo("Second");
    }

    [Test]
    [Category("Shield")]
    public async Task ShieldOff_ReadByClipMate_IsNotAPaste()
    {
        // Arrange
        UseConfiguration(new PreferencesConfiguration { PowerPasteShield = false, PowerPasteDelay = 0 });
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);

        // Act
        _clipboard.ReadByClipMate();

        // Assert
        await Assert.That(WaitingClip(service)?.TextContent).IsEqualTo("First");
    }

    [Test]
    [Category("Toggle")]
    public async Task Toggle_WithNothingSelected_LeavesPowerPasteOff()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.ToggleAsync(_ => []);

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
