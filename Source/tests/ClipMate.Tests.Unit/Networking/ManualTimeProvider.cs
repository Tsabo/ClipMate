namespace ClipMate.Tests.Unit.Networking;

/// <summary>
/// A clock tests move forward by hand. Its timers (and so <c>Task.Delay</c> with this provider) fire only when
/// <see cref="Advance" /> moves the clock past their due time, never on their own.
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly Lock _lock = new();
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Gets how many timers are waiting to fire.
    /// </summary>
    public int PendingTimers
    {
        get
        {
            lock (_lock)
                return _timers.Count(p => p.DueAt != null);
        }
    }

    /// <summary>
    /// Moves the clock forward and fires every timer that is then due (a periodic timer at most once per call).
    /// </summary>
    public void Advance(TimeSpan by)
    {
        List<ManualTimer> due;
        lock (_lock)
        {
            _now += by;
            due = _timers.Where(p => p.DueAt <= _now).ToList();
            foreach (var item in due)
                item.DueAt = item.Period == Timeout.InfiniteTimeSpan || item.Period <= TimeSpan.Zero ? null : _now + item.Period;
        }

        foreach (var item in due)
            item.Fire();
    }

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow()
    {
        lock (_lock)
            return _now;
    }

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    private sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        public DateTimeOffset? DueAt { get; set; }

        public TimeSpan Period { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (owner._lock)
            {
                DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : owner._now + dueTime;
                Period = period;
                if (!owner._timers.Contains(this))
                    owner._timers.Add(this);
            }

            return true;
        }

        public void Fire() => callback(state);

        public void Dispose()
        {
            lock (owner._lock)
                owner._timers.Remove(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
