using ClipMate.Platform.Services;
using ClipMate.Tests.Unit.Networking;

namespace ClipMate.Tests.Unit.Platform;

public class ClipboardCaptureSchedulerTests
{
    private readonly List<ClipboardCaptureTrace> _traces = [];
    private readonly ManualTimeProvider _time = new();
    private int _captures;

    [Test]
    public async Task OnClipboardUpdate_BurstOfUpdates_CapturesOnceAfterTheLastOne()
    {
        // Arrange
        using var scheduler = CreateScheduler(delayMs: 250, settleTimeMs: 100);

        // Act: a source adds its formats one at a time
        scheduler.OnClipboardUpdate();
        _time.Advance(TimeSpan.FromMilliseconds(100));
        scheduler.OnClipboardUpdate();
        _time.Advance(TimeSpan.FromMilliseconds(100));
        scheduler.OnClipboardUpdate();
        _time.Advance(TimeSpan.FromMilliseconds(249));
        var capturesBeforeTheDelayEnds = _captures;
        _time.Advance(TimeSpan.FromMilliseconds(1));

        // Assert
        await Assert.That(capturesBeforeTheDelayEnds).IsEqualTo(0);
        await Assert.That(_captures).IsEqualTo(1);
        await Assert.That(_traces).IsEquivalentTo([new ClipboardCaptureTrace(3, true, "SOURCE", ClipboardCaptureOutcome.Captured)]);
    }

    [Test]
    public async Task OnClipboardUpdate_ZeroDelay_CapturesImmediately()
    {
        // Arrange
        using var scheduler = CreateScheduler(delayMs: 0, settleTimeMs: 100);

        // Act
        scheduler.OnClipboardUpdate();

        // Assert
        await Assert.That(_captures).IsEqualTo(1);
        await Assert.That(_traces).IsEquivalentTo([new ClipboardCaptureTrace(1, false, "SOURCE", ClipboardCaptureOutcome.Captured)]);
    }

    [Test]
    public async Task OnClipboardUpdate_WithinTheSettleTimeOfTheLastCapture_IsDroppedAndTraced()
    {
        // Arrange
        using var scheduler = CreateScheduler(delayMs: 50, settleTimeMs: 100);
        scheduler.OnClipboardUpdate();
        _time.Advance(TimeSpan.FromMilliseconds(50));

        // Act: the next capture would start 50 ms after the first one
        scheduler.OnClipboardUpdate();
        _time.Advance(TimeSpan.FromMilliseconds(50));

        // Assert
        await Assert.That(_captures).IsEqualTo(1);
        await Assert.That(_traces[^1]).IsEqualTo(new ClipboardCaptureTrace(1, true, null, ClipboardCaptureOutcome.SettleGap));
    }

    [Test]
    public async Task OnClipboardUpdate_AfterTheSettleTime_Captures()
    {
        // Arrange
        using var scheduler = CreateScheduler(delayMs: 50, settleTimeMs: 100);
        scheduler.OnClipboardUpdate();
        _time.Advance(TimeSpan.FromMilliseconds(50));
        _time.Advance(TimeSpan.FromMilliseconds(50));

        // Act
        scheduler.OnClipboardUpdate();
        _time.Advance(TimeSpan.FromMilliseconds(50));

        // Assert
        await Assert.That(_captures).IsEqualTo(2);
    }

    [Test]
    public async Task OnClipboardUpdate_WhileACaptureIsRunning_CapturesAgainAfterTheDelay()
    {
        // Arrange: the first capture waits for a slow source
        var slowRead = new TaskCompletionSource<ClipboardCaptureResult>();
        var reads = new Queue<Task<ClipboardCaptureResult>>([slowRead.Task, Task.FromResult(new ClipboardCaptureResult(ClipboardCaptureOutcome.Captured, "SOURCE"))]);
        using var scheduler = CreateScheduler(250, 100, () => reads.Dequeue());
        scheduler.OnClipboardUpdate();
        _time.Advance(TimeSpan.FromMilliseconds(250));

        // Act: the clipboard changes again while it is read, then the read finishes
        scheduler.OnClipboardUpdate();
        _time.Advance(TimeSpan.FromMilliseconds(250));
        var capturesWhileReading = _captures;
        slowRead.SetResult(new ClipboardCaptureResult(ClipboardCaptureOutcome.Captured, "SOURCE"));
        _time.Advance(TimeSpan.FromMilliseconds(250));

        // Assert
        await Assert.That(capturesWhileReading).IsEqualTo(1);
        await Assert.That(_captures).IsEqualTo(2);
        await Assert.That(_traces.Select(p => p.Outcome)).IsEquivalentTo([ClipboardCaptureOutcome.Captured, ClipboardCaptureOutcome.Captured]);
    }

    [Test]
    [Arguments(ClipboardCaptureScheduler.MaxSettleGapDrops - 1, 2)]
    [Arguments(ClipboardCaptureScheduler.MaxSettleGapDrops, 1)]
    public async Task OnClipboardUpdate_AfterManySettleGapDrops_StopsCapturing(int drops, int expectedCaptures)
    {
        // Arrange: two clipboard tools copying back and forth, every change within the settle time of the capture
        using var scheduler = CreateScheduler(delayMs: 0, settleTimeMs: 100);
        scheduler.OnClipboardUpdate();
        for (var i = 0; i < drops; i++)
            scheduler.OnClipboardUpdate();

        // Act: a change well after the settle time
        _time.Advance(TimeSpan.FromSeconds(1));
        scheduler.OnClipboardUpdate();

        // Assert
        await Assert.That(scheduler.SettleGapDrops).IsEqualTo(drops);
        await Assert.That(_captures).IsEqualTo(expectedCaptures);
        if (expectedCaptures == 1)
            await Assert.That(_traces[^1].Outcome).IsEqualTo(ClipboardCaptureOutcome.TooManySettleGapDrops);
    }

    private ClipboardCaptureScheduler CreateScheduler(int delayMs, int settleTimeMs, Func<Task<ClipboardCaptureResult>>? read = null)
    {
        read ??= () => Task.FromResult(new ClipboardCaptureResult(ClipboardCaptureOutcome.Captured, "SOURCE"));

        return new ClipboardCaptureScheduler(
            _time,
            () => TimeSpan.FromMilliseconds(delayMs),
            () => TimeSpan.FromMilliseconds(settleTimeMs),
            action => action(),
            () =>
            {
                _captures++;
                return read();
            },
            _traces.Add);
    }
}
