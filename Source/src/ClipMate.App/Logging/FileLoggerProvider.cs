using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClipMate.App.Logging;

/// <summary>
/// Simple file-based logger provider for application logging.
/// </summary>
public class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logDirectory;
    private readonly Func<DateTime, string> _fileNameFormat;
    private readonly Dictionary<string, FileLogger> _loggers = new();
    private readonly object _lock = new();

    public FileLoggerProvider(string logDirectory, Func<DateTime, string> fileNameFormat)
    {
        _logDirectory = logDirectory ?? throw new ArgumentNullException(nameof(logDirectory));
        _fileNameFormat = fileNameFormat ?? throw new ArgumentNullException(nameof(fileNameFormat));

        if (!Directory.Exists(_logDirectory))
        {
            Directory.CreateDirectory(_logDirectory);
        }
    }

    public ILogger CreateLogger(string categoryName)
    {
        lock (_lock)
        {
            if (!_loggers.TryGetValue(categoryName, out var logger))
            {
                var logFilePath = Path.Combine(_logDirectory, _fileNameFormat(DateTime.Now));
                logger = new FileLogger(categoryName, logFilePath);
                _loggers[categoryName] = logger;
            }
            return logger;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var logger in _loggers.Values)
            {
                logger.Dispose();
            }
            _loggers.Clear();
        }
    }

    private class FileLogger : ILogger, IDisposable
    {
        private readonly string _categoryName;
        private readonly string _logFilePath;
        private readonly object _lock = new();
        private StreamWriter? _writer;

        public FileLogger(string categoryName, string logFilePath)
        {
            _categoryName = categoryName;
            _logFilePath = logFilePath;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            try
            {
                lock (_lock)
                {
                    _writer ??= new StreamWriter(_logFilePath, append: true) { AutoFlush = true };

                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var level = logLevel.ToString().ToUpper();
                    var message = formatter(state, exception);
                    var logEntry = $"[{timestamp}] [{level}] [{_categoryName}] {message}";

                    _writer.WriteLine(logEntry);

                    if (exception != null)
                    {
                        _writer.WriteLine(exception.ToString());
                    }
                }
            }
            catch
            {
                // Ignore logging errors to prevent cascading failures
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _writer?.Dispose();
                _writer = null;
            }
        }
    }
}

/// <summary>
/// Extension methods for file logging configuration.
/// </summary>
public static class FileLoggerExtensions
{
    public static ILoggingBuilder AddFile(this ILoggingBuilder builder, string logDirectory, Action<FileLoggerOptions>? configure = null)
    {
        var options = new FileLoggerOptions();
        configure?.Invoke(options);

#pragma warning disable CA2000 // Dispose objects before losing scope - DI container manages lifetime
        var provider = new FileLoggerProvider(logDirectory, options.FileNameFormat);
#pragma warning restore CA2000
        builder.Services.AddSingleton<ILoggerProvider>(provider);
        return builder;
    }
}

/// <summary>
/// Configuration options for file logging.
/// </summary>
public class FileLoggerOptions
{
    /// <summary>
    /// Gets or sets the file name format function.
    /// </summary>
    public Func<DateTime, string> FileNameFormat { get; set; } = date => $"app_{date:yyyyMMdd}.log";
}
