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
        _clipboard.ReadByTheTarget();

        // Assert
        await Assert.That(service.CurrentPosition).IsEqualTo(0);
        _clock.Advance(TimeSpan.FromMilliseconds(199));
        await Assert.That(service.CurrentPosition).IsEqualTo(0);
        _clock.Advance(TimeSpan.FromMilliseconds(1));
        await Assert.That(service.CurrentPosition).IsEqualTo(1);
    }

    [Test]
    [Category("Clipboard")]
    public async Task Read_ByTheTargetAgainBeforeTheDelay_GetsThePastedClipAndIsNotASecondPaste()
    {
        // Arrange - Word reads again 126 ms after a paste; the default delay is 250 ms
        UseConfiguration(new PreferencesConfiguration { PowerPasteDelay = 250 });
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second"), CreateTestClip("Third")], PowerPasteDirection.Down);
        _clipboard.ReadByTheTarget();

        // Act
        _clock.Advance(TimeSpan.FromMilliseconds(126));
        var served = _clipboard.ReadByTheTarget();
        _clock.Advance(TimeSpan.FromMilliseconds(124));

        // Assert
        await Assert.That(served).IsTrue();
        await Assert.That(service.CurrentPosition).IsEqualTo(1);
        await Assert.That(WaitingClip(service)?.TextContent).IsEqualTo("Second");
    }

    [Test]
    [Category("Clipboard")]
    public async Task PowerPasteDelay_DefaultsTo250Milliseconds() =>
        await Assert.That(new PreferencesConfiguration().PowerPasteDelay).IsEqualTo(250);

    [Test]
    [Category("PasteDetection")]
    public async Task Read_ByABackgroundProgram_IsRefusedAndChangesNothing()
    {
        // Arrange - Remote Desktop's client and clipboard history read every offer from their own windows
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);

        // Act
        var served = _clipboard.ReadInTheBackground();
        _clock.Advance(TimeSpan.FromSeconds(5));

        // Assert - nothing offered again, so nothing has a reason to read again
        await Assert.That(served).IsFalse();
        await Assert.That(_clipboard.Offers).Count().IsEqualTo(1);
        await Assert.That(service.CurrentPosition).IsEqualTo(0);
    }

    [Test]
    [Category("PasteDetection")]
    public async Task Read_ByABackgroundProgramOverAndOver_IsRefusedEachTimeAndTheNextPasteStillMovesOn()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);
        for (var i = 0; i < 100; i++)
        {
            _clipboard.ReadInTheBackground(i % 2 == 0 ? "msrdc" : "svchost");
            _clock.Advance(TimeSpan.FromMilliseconds(10));
        }

        // Act
        await PasteAsync();

        // Assert
        await Assert.That(_clipboard.Refusals).IsEqualTo(100);
        await Assert.That(_clipboard.Offers).Count().IsEqualTo(2);
        await Assert.That(WaitingClip(service)?.TextContent).IsEqualTo("Second");
    }

    [Test]
    [Category("PasteDetection")]
    public async Task Read_WithoutAWindow_IsAPaste()
    {
        // Arrange - browsers, Electron programs and Windows Terminal paste without a window
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);

        // Act
        var served = _clipboard.ReadWithoutAWindow();

        // Assert
        await Assert.That(served).IsTrue();
        await Assert.That(WaitingClip(service)?.TextContent).IsEqualTo("Second");
    }

    [Test]
    [Category("PasteDetection")]
    public async Task Read_ForAStoreAppInTheForeground_IsAPaste()
    {
        // Arrange - Calculator's paste is read by a Windows service while ApplicationFrameHost is in the foreground
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);

        // Act
        _clipboard.ReadForAStoreApp();

        // Assert
        await Assert.That(WaitingClip(service)?.TextContent).IsEqualTo("Second");
    }

    [Test]
    [Category("PasteDetection")]
    public async Task Read_ByClipMateOrWithItInTheForeground_IsRefused()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);

        // Act
        var byClipMate = _clipboard.ReadByClipMate();
        var withClipMateInFront = _clipboard.ReadWithoutAWindowWithClipMateInTheForeground();

        // Assert
        await Assert.That(byClipMate).IsFalse();
        await Assert.That(withClipMateInFront).IsFalse();
        await Assert.That(WaitingClip(service)?.TextContent).IsEqualTo("First");
    }

    [Test]
    [Category("PasteDetection")]
    public async Task Read_ByABackgroundProgram_ThenWithoutAWindow_IsAPaste()
    {
        // Arrange - the refused read leaves the clip waiting, so the paste that follows is still heard
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);
        _clipboard.ReadInTheBackground("svchost");

        // Act
        _clipboard.ReadWithoutAWindow();

        // Assert
        await Assert.That(WaitingClip(service)?.TextContent).IsEqualTo("Second");
    }

    /// <summary>
    /// Moves the clock in steps, so a wait started when an earlier one ends also runs out.
    /// </summary>
    private void LetTimeGoBy()
    {
        for (var i = 0; i < 20; i++)
            _clock.Advance(TimeSpan.FromMilliseconds(250));
    }

    [Test]
    [Category("Clipboard")]
    public async Task Offer_WhenTheClipboardCannotBeOpened_TriesAgainUntilItIsOffered()
    {
        // Arrange - the previous clip stays on the clipboard until the next one gets there, so the next paste repeats it
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);
        var offers = _clipboard.Offers.Count;
        _clipboard.OffersToFail = 2;

        // Act
        await PasteAsync();
        var offersAfterTheFailure = _clipboard.Offers.Count;
        LetTimeGoBy();

        // Assert
        await Assert.That(offersAfterTheFailure).IsEqualTo(offers);
        await Assert.That(_clipboard.OffersToFail).IsEqualTo(0);
        await Assert.That(_clipboard.Offers.Count).IsEqualTo(offers + 1);
    }

    [Test]
    [Category("Clipboard")]
    public async Task Offer_WhenTheClipboardCannotBeOpened_StopsTryingWhenPowerPasteStops()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);
        var offers = _clipboard.Offers.Count;
        _clipboard.OffersToFail = 1;
        await PasteAsync();

        // Act
        service.Stop();
        _clock.Advance(TimeSpan.FromSeconds(5));

        // Assert
        await Assert.That(_clipboard.Offers.Count).IsEqualTo(offers);
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
    public async Task Read_AfterStop_IsNotReported()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);
        service.Stop(false);
        var offers = _clipboard.Offers.Count;

        // Act
        _clipboard.ReadByTheTarget();
        _clock.Advance(TimeSpan.FromSeconds(1));

        // Assert
        await Assert.That(service.State).IsEqualTo(PowerPasteState.Inactive);
        await Assert.That(_clipboard.Offers.Count).IsEqualTo(offers);
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
    [Category("Clipboard")]
    public async Task Read_ByTheTarget300MillisecondsAfterAPaste_IsAnotherPaste()
    {
        // Arrange - the manual says pasting "very rapidly"
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second"), CreateTestClip("Third")], PowerPasteDirection.Down);
        await PasteAsync();

        // Act
        _clock.Advance(TimeSpan.FromMilliseconds(300));
        _clipboard.ReadByTheTarget();

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
        var served = _clipboard.ReadInTheBackground();

        // Assert
        await Assert.That(served).IsTrue();
        await Assert.That(WaitingClip(service)?.TextContent).IsEqualTo("Second");
    }

    [Test]
    [Category("Shield")]
    public async Task ShieldOff_ReadByClipMate_IsRefused()
    {
        // Arrange
        UseConfiguration(new PreferencesConfiguration { PowerPasteShield = false, PowerPasteDelay = 0 });
        var service = CreateService();
        await service.StartAsync([CreateTestClip("First"), CreateTestClip("Second")], PowerPasteDirection.Down);

        // Act
        var served = _clipboard.ReadByClipMate();

        // Assert
        await Assert.That(served).IsFalse();
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
