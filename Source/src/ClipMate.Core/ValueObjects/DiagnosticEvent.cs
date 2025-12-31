using Microsoft.Extensions.Logging;

namespace ClipMate.Core.ValueObjects;

/// <summary>
/// Represents a diagnostic event captured from the logging system.
/// </summary>
public sealed record DiagnosticEvent
{
    /// <summary>
    /// Gets the timestamp when the event occurred.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets the log level of the event.
    /// </summary>
    public LogLevel Level { get; init; }

    /// <summary>
    /// Gets the message content.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the category/logger name.
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Gets the emoji icon for the log level.
    /// </summary>
    public string IconGlyph => Level switch
    {
        LogLevel.Trace => "🔍",
        LogLevel.Debug => "🐛",
        LogLevel.Information => "ℹ️",
        LogLevel.Warning => "⚠️",
        LogLevel.Error => "❌",
        LogLevel.Critical => "💀",
        var _ => "📋",
    };

    /// <summary>
    /// Gets the formatted display string for the event.
    /// </summary>
    public string DisplayText => $"{IconGlyph} {Timestamp:MM/dd/yyyy h:mm:ss tt}: {Message}";

    /// <summary>
    /// Gets the formatted timestamp string.
    /// </summary>
    public string TimestampText => Timestamp.ToString("MM/dd/yyyy h:mm:ss:fff tt");
}
