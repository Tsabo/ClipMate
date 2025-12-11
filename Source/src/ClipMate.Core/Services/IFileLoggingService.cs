namespace ClipMate.Core.Services;

/// <summary>
/// Service for managing file-based logging operations.
/// Provides access to log files for viewing and troubleshooting.
/// </summary>
public interface IFileLoggingService
{
    /// <summary>
    /// Gets the directory where log files are stored.
    /// </summary>
    string LogDirectory { get; }

    /// <summary>
    /// Gets all log files in the log directory, ordered by creation date (newest first).
    /// </summary>
    /// <returns>Collection of log file paths.</returns>
    IReadOnlyList<string> GetLogFiles();

    /// <summary>
    /// Gets the most recent log file path.
    /// </summary>
    /// <returns>Path to the most recent log file, or null if no logs exist.</returns>
    string? GetCurrentLogFile();

    /// <summary>
    /// Opens the log directory in Windows Explorer.
    /// </summary>
    void OpenLogDirectory();

    /// <summary>
    /// Reads the contents of a log file.
    /// </summary>
    /// <param name="logFilePath">Path to the log file.</param>
    /// <returns>Log file contents.</returns>
    Task<string> ReadLogFileAsync(string logFilePath);

    /// <summary>
    /// Deletes old log files beyond the specified retention days.
    /// </summary>
    /// <param name="retentionDays">Number of days to retain logs.</param>
    /// <returns>Number of log files deleted.</returns>
    int DeleteOldLogs(int retentionDays);

    /// <summary>
    /// Creates a ZIP archive of all current log files for troubleshooting submission.
    /// </summary>
    /// <param name="outputPath">Path where the ZIP file should be created.</param>
    /// <returns>Path to the created ZIP file.</returns>
    Task<string> CreateLogArchiveAsync(string outputPath);

    /// <summary>
    /// Gets the total size of all log files in bytes.
    /// </summary>
    /// <returns>Total size of log files in bytes.</returns>
    long GetTotalLogSize();
}
