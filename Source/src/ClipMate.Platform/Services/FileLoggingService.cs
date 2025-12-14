using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using ClipMate.Core.Services;
using Microsoft.Extensions.Logging;

namespace ClipMate.Platform.Services;

/// <summary>
/// Implementation of file-based logging service.
/// Manages log files in %LOCALAPPDATA%\ClipMate\Logs with automatic rotation.
/// </summary>
public class FileLoggingService : IFileLoggingService
{
    private readonly ILogger<FileLoggingService> _logger;

    public FileLoggingService(ILogger<FileLoggingService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize log directory
        LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClipMate",
            "Logs");

        try
        {
            Directory.CreateDirectory(LogDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create log directory: {LogDirectory}", LogDirectory);
        }
    }

    /// <inheritdoc />
    public string LogDirectory { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> GetLogFiles()
    {
        try
        {
            if (!Directory.Exists(LogDirectory))
                return Array.Empty<string>();

            var logFiles = Directory.GetFiles(LogDirectory, "clipmate-*.log")
                .Union(Directory.GetFiles(LogDirectory, "clipmate-*.txt"))
                .OrderByDescending(p => new FileInfo(p).CreationTime)
                .ToList();

            return logFiles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get log files from {LogDirectory}", LogDirectory);

            return Array.Empty<string>();
        }
    }

    /// <inheritdoc />
    public string? GetCurrentLogFile()
    {
        var logFiles = GetLogFiles();

        return logFiles.Count > 0
            ? logFiles[0]
            : null;
    }

    /// <inheritdoc />
    public void OpenLogDirectory()
    {
        try
        {
            // Ensure directory exists
            Directory.CreateDirectory(LogDirectory);

            // Open in Windows Explorer
            Process.Start(new ProcessStartInfo
            {
                FileName = LogDirectory,
                UseShellExecute = true,
                Verb = "open",
            });

            _logger.LogInformation("Opened log directory in Explorer: {LogDirectory}", LogDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open log directory: {LogDirectory}", LogDirectory);

            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string> ReadLogFileAsync(string logFilePath)
    {
        try
        {
            if (!File.Exists(logFilePath))
                throw new FileNotFoundException($"Log file not found: {logFilePath}");

            // Read with FileShare.ReadWrite to allow concurrent access from logger
            await using var stream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);

            return await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read log file: {LogFilePath}", logFilePath);

            throw;
        }
    }

    /// <inheritdoc />
    public int DeleteOldLogs(int retentionDays)
    {
        try
        {
            if (!Directory.Exists(LogDirectory))
                return 0;

            var cutoffDate = DateTime.Now.AddDays(-retentionDays);
            var logFiles = GetLogFiles();
            var deletedCount = 0;

            foreach (var item in logFiles)
            {
                try
                {
                    var fileInfo = new FileInfo(item);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        File.Delete(item);
                        deletedCount++;
                        _logger.LogDebug("Deleted old log file: {FileName}", fileInfo.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete log file: {LogFile}", item);
                }
            }

            if (deletedCount > 0)
                _logger.LogInformation("Deleted {Count} old log files (retention: {Days} days)", deletedCount, retentionDays);

            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete old logs");

            return 0;
        }
    }

    /// <inheritdoc />
    public async Task<string> CreateLogArchiveAsync(string outputPath)
    {
        try
        {
            var logFiles = GetLogFiles();

            if (logFiles.Count == 0)
                throw new InvalidOperationException("No log files to archive");

            // Ensure output directory exists
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir))
                Directory.CreateDirectory(outputDir);

            // Delete existing archive if present
            if (File.Exists(outputPath))
                File.Delete(outputPath);

            // Create ZIP archive
            using (var archive = ZipFile.Open(outputPath, ZipArchiveMode.Create))
            {
                foreach (var item in logFiles)
                {
                    try
                    {
                        var fileName = Path.GetFileName(item);

                        // Copy file to temp location first to avoid locking issues
                        var tempFile = Path.GetTempFileName();

                        await using (var sourceStream = new FileStream(item, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        await using (var destStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
                        {
                            await sourceStream.CopyToAsync(destStream);
                        }

                        // Add temp file to archive
                        archive.CreateEntryFromFile(tempFile, fileName, CompressionLevel.Optimal);

                        // Clean up temp file
                        File.Delete(tempFile);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to add log file to archive: {LogFile}", item);
                    }
                }
            }

            var archiveSize = new FileInfo(outputPath).Length;
            _logger.LogInformation("Created log archive: {ArchivePath} ({Size} bytes)", outputPath, archiveSize);

            return outputPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create log archive: {OutputPath}", outputPath);

            throw;
        }
    }

    /// <inheritdoc />
    public long GetTotalLogSize()
    {
        try
        {
            if (!Directory.Exists(LogDirectory))
                return 0;

            var logFiles = GetLogFiles();

            return logFiles.Sum(p => new FileInfo(p).Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate total log size");

            return 0;
        }
    }
}
