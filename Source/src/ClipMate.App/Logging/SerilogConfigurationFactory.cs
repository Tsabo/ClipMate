using System.IO;
using ClipMate.App.Services;
using ClipMate.Core.Models.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using ILogger = Serilog.ILogger;

namespace ClipMate.App.Logging;

/// <summary>
/// Factory for creating and configuring Serilog ILogger instances.
/// Consolidates Serilog configuration to avoid duplication.
/// </summary>
public static class SerilogConfigurationFactory
{
    /// <summary>
    /// Creates a Serilog logger with the specified log level for early application startup.
    /// Used before full configuration is loaded.
    /// </summary>
    /// <param name="eventLogSink">The event log sink for capturing logs in-memory.</param>
    /// <returns>Configured Serilog ILogger instance.</returns>
    public static ILogger CreateEarlyLogger(EventLogSink eventLogSink)
    {
        var (logDirectory, logFilePath) = GetLogPaths();

        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .WriteTo.Debug()
            .WriteTo.Console()
            .WriteTo.Sink(new EventLogSerilogSink(eventLogSink))
            .WriteTo.File(
                logFilePath,
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: 10_485_760, // 10 MB
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{ThreadId}] {SourceContext} - {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    /// <summary>
    /// Creates a Serilog logger configured with the specified log level from application configuration.
    /// Used after configuration is loaded.
    /// </summary>
    /// <param name="eventLogSink">The event log sink for capturing logs in-memory.</param>
    /// <param name="configuration">Application configuration containing log level preferences.</param>
    /// <returns>Configured Serilog ILogger instance.</returns>
    public static ILogger CreateConfiguredLogger(EventLogSink eventLogSink, ClipMateConfiguration configuration)
    {
        var (logDirectory, logFilePath) = GetLogPaths();

        return new LoggerConfiguration()
            .MinimumLevel.Is(ConvertToSerilogLevel(configuration.Preferences.LogLevel))
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .WriteTo.Debug()
            .WriteTo.Console()
            .WriteTo.Sink(new EventLogSerilogSink(eventLogSink))
            .WriteTo.File(
                logFilePath,
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: 10_485_760, // 10 MB
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{ThreadId}] {SourceContext} - {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    /// <summary>
    /// Gets the log directory and file path for Serilog configuration.
    /// Creates the directory if it doesn't exist.
    /// </summary>
    /// <returns>Tuple of (logDirectory, logFilePath).</returns>
    private static (string logDirectory, string logFilePath) GetLogPaths()
    {
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClipMate",
            "Logs");

        try
        {
            Directory.CreateDirectory(logDirectory);
        }
        catch
        {
            // Can't create log directory - Serilog will handle the error
        }

        var logFilePath = Path.Join(logDirectory, "clipmate-.log");

        return (logDirectory, logFilePath);
    }

    /// <summary>
    /// Converts Microsoft.Extensions.Logging.LogLevel to Serilog LogEventLevel.
    /// </summary>
    /// <param name="level">Microsoft.Extensions.Logging.LogLevel from configuration.</param>
    /// <returns>Corresponding Serilog LogEventLevel.</returns>
    private static LogEventLevel ConvertToSerilogLevel(LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => LogEventLevel.Verbose,
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Information => LogEventLevel.Information,
            LogLevel.Warning => LogEventLevel.Warning,
            LogLevel.Error => LogEventLevel.Error,
            LogLevel.Critical => LogEventLevel.Fatal,
            LogLevel.None => LogEventLevel.Fatal,
            var _ => LogEventLevel.Information,
        };
    }
}
