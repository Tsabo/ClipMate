namespace ClipMate.Platform.Services;

/// <summary>
/// What became of a clipboard change.
/// </summary>
internal enum ClipboardCaptureOutcome
{
    Captured,
    SettleGap,
    Suspended,
    IgnoreFlag,
    Empty,
    Unsupported,
    OwnCopy,
    Duplicate,
    Failed,
}

/// <summary>
/// The result of reading the clipboard after a change: what became of it and which program owned the clipboard.
/// </summary>
internal readonly record struct ClipboardCaptureResult(ClipboardCaptureOutcome Outcome, string? Owner);

/// <summary>
/// One capture as traced: the change notifications it covers, whether the delay timer started it (rather than a zero
/// delay), the clipboard owner and the outcome.
/// </summary>
internal readonly record struct ClipboardCaptureTrace(int Notifications, bool TimerFired, string? Owner, ClipboardCaptureOutcome Outcome);

/// <summary>
/// Decides when a clipboard change is read, as ClipMate 7 does: each change notification restarts one timer, so a
/// source that adds its formats one at a time is captured once, after its last update; a capture starting within the
/// settle time of the previous one is dropped. Every member runs on the clipboard thread; the timer posts back to it.
/// </summary>
internal sealed class ClipboardCaptureScheduler : IDisposable
{
    private readonly Func<Task<ClipboardCaptureResult>> _capture;
    private readonly Func<TimeSpan> _delay;
    private readonly Action<Action> _post;
    private readonly Func<TimeSpan> _settleTime;
    private readonly TimeProvider _timeProvider;
    private readonly ITimer _timer;
    private readonly Action<ClipboardCaptureTrace> _trace;
    private bool _capturing;
    private DateTimeOffset? _lastCaptureEnd;
    private int _notifications;
    private bool _rerunWhenDone;
    private DateTimeOffset? _timerDueAt;

    /// <param name="timeProvider">The clock and timer source.</param>
    /// <param name="delay">The delay after a change before reading the clipboard.</param>
    /// <param name="settleTime">How long after a capture another one is dropped.</param>
    /// <param name="post">Runs an action on the clipboard thread.</param>
    /// <param name="capture">Reads the clipboard; runs on the clipboard thread.</param>
    /// <param name="trace">Records each capture, including the dropped ones.</param>
    public ClipboardCaptureScheduler(TimeProvider timeProvider, Func<TimeSpan> delay, Func<TimeSpan> settleTime, Action<Action> post,
        Func<Task<ClipboardCaptureResult>> capture, Action<ClipboardCaptureTrace> trace)
    {
        _timeProvider = timeProvider;
        _delay = delay;
        _settleTime = settleTime;
        _post = post;
        _capture = capture;
        _trace = trace;
        _timer = timeProvider.CreateTimer(_ => _post(OnTimerFired), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public void Dispose() => _timer.Dispose();

    /// <summary>
    /// Handles one <c>WM_CLIPBOARDUPDATE</c>.
    /// </summary>
    public void OnClipboardUpdate()
    {
        _notifications++;
        ScheduleCapture();
    }

    private void ScheduleCapture()
    {
        var delay = _delay();
        if (delay > TimeSpan.Zero)
        {
            RestartTimer(delay);
            return;
        }

        StopTimer();
        _ = CaptureAsync(false);
    }

    private void OnTimerFired()
    {
        if (_timerDueAt is not { } dueAt)
            return;

        // A notification that arrived after the timer fired but before this ran has restarted it
        var remaining = dueAt - _timeProvider.GetUtcNow();
        if (remaining > TimeSpan.Zero)
        {
            _timer.Change(remaining, Timeout.InfiniteTimeSpan);
            return;
        }

        _timerDueAt = null;
        _ = CaptureAsync(true);
    }

    private async Task CaptureAsync(bool timerFired)
    {
        if (_capturing)
        {
            _rerunWhenDone = true;
            return;
        }

        var notifications = _notifications;
        _notifications = 0;

        if (_lastCaptureEnd is { } lastCaptureEnd && _timeProvider.GetUtcNow() - lastCaptureEnd < _settleTime())
        {
            _trace(new ClipboardCaptureTrace(notifications, timerFired, null, ClipboardCaptureOutcome.SettleGap));
            return;
        }

        _capturing = true;
        ClipboardCaptureResult result;
        try
        {
            result = await _capture();
        }
        catch (Exception)
        {
            result = new ClipboardCaptureResult(ClipboardCaptureOutcome.Failed, null);
        }
        finally
        {
            _capturing = false;
            _lastCaptureEnd = _timeProvider.GetUtcNow();
        }

        _trace(new ClipboardCaptureTrace(notifications, timerFired, result.Owner, result.Outcome));

        if (!_rerunWhenDone)
            return;

        _rerunWhenDone = false;
        ScheduleCapture();
    }

    private void RestartTimer(TimeSpan delay)
    {
        _timerDueAt = _timeProvider.GetUtcNow() + delay;
        _timer.Change(delay, Timeout.InfiniteTimeSpan);
    }

    private void StopTimer()
    {
        _timerDueAt = null;
        _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }
}
