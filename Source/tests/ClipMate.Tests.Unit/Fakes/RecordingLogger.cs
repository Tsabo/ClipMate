using Microsoft.Extensions.Logging;

namespace ClipMate.Tests.Unit.Fakes;

/// <summary>
/// <see cref="ILogger{TCategoryName}" /> that keeps every entry, for tests of what a class logs.
/// </summary>
public sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly Lock _lock = new();
    private readonly List<LogEntry> _entries = [];

    /// <summary>
    /// The entries logged so far, oldest first.
    /// </summary>
    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (_lock)
                return _entries.ToList();
        }
    }

    /// <summary>
    /// The messages logged at <paramref name="level" />, oldest first.
    /// </summary>
    public IReadOnlyList<string> MessagesAt(LogLevel level) => Entries.Where(p => p.Level == level).Select(p => p.Message).ToList();

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (_lock)
            _entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
    }
}

/// <summary>
/// One entry a <see cref="RecordingLogger{T}" /> kept: its level, formatted message and exception.
/// </summary>
public sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
