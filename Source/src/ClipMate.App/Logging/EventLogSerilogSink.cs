using ClipMate.Core.Services;
using ClipMate.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;

namespace ClipMate.App.Logging;

/// <summary>
/// Serilog sink that writes log events to the EventLogSink for display in the Event Log dialog.
/// Captures all log levels to allow runtime filtering in the UI.
/// </summary>
public sealed class EventLogSerilogSink : ILogEventSink
{
    private readonly IEventLogSink _eventLogSink;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventLogSerilogSink" /> class.
    /// </summary>
    /// <param name="eventLogSink">The event log sink to write to.</param>
    public EventLogSerilogSink(IEventLogSink eventLogSink)
    {
        _eventLogSink = eventLogSink ?? throw new ArgumentNullException(nameof(eventLogSink));
    }

    /// <inheritdoc />
    public void Emit(LogEvent logEvent)
    {
        var message = logEvent.RenderMessage();
        if (logEvent.Exception != null)
            message = $"{message} - {logEvent.Exception.Message}";

        var diagnosticEvent = new DiagnosticEvent
        {
            Timestamp = logEvent.Timestamp.LocalDateTime,
            Level = ConvertLogLevel(logEvent.Level),
            Message = message,
            Category = ExtractCategory(logEvent),
        };

        _eventLogSink.Add(diagnosticEvent);
    }

    /// <summary>
    /// Converts Serilog LogEventLevel to Microsoft.Extensions.Logging LogLevel.
    /// </summary>
    private static LogLevel ConvertLogLevel(LogEventLevel level) =>
        level switch
        {
            LogEventLevel.Verbose => LogLevel.Trace,
            LogEventLevel.Debug => LogLevel.Debug,
            LogEventLevel.Information => LogLevel.Information,
            LogEventLevel.Warning => LogLevel.Warning,
            LogEventLevel.Error => LogLevel.Error,
            LogEventLevel.Fatal => LogLevel.Critical,
            _ => LogLevel.Information,
        };

    /// <summary>
    /// Extracts the category (source context) from the log event.
    /// </summary>
    private static string ExtractCategory(LogEvent logEvent)
    {
        if (logEvent.Properties.TryGetValue("SourceContext", out var sourceContext))
        {
            var fullName = sourceContext.ToString().Trim('"');
            var lastDot = fullName.LastIndexOf('.');
            return lastDot >= 0 ? fullName[(lastDot + 1)..] : fullName;
        }

        return "Unknown";
    }
}
