namespace ClipMate.Core.Models.Configuration;

/// <summary>
/// Configuration for application directories.
/// Supports environment variable expansion like %APPDATA%, %LOCALAPPDATA%, etc.
/// </summary>
public class DirectoriesConfiguration
{
    /// <summary>
    /// Gets or sets the directory where log files are stored.
    /// Default: %LOCALAPPDATA%\ClipMate\Logs
    /// </summary>
    public string? LogDirectory { get; set; }

    /// <summary>
    /// Gets or sets the directory for temporary files.
    /// Default: %TEMP%\ClipMate
    /// </summary>
    public string? TempDirectory { get; set; }

    /// <summary>
    /// Gets or sets the directory where templates are stored.
    /// Default: %APPDATA%\ClipMate\Templates
    /// </summary>
    public string? TemplatesDirectory { get; set; }

    /// <summary>
    /// Gets or sets the directory for exported clips.
    /// Default: %USERPROFILE%\Documents\ClipMate\Exports
    /// </summary>
    public string? ExportDirectory { get; set; }

    /// <summary>
    /// Gets or sets the directory for sound files.
    /// Default: [Application Directory]\Sounds
    /// </summary>
    public string? SoundsDirectory { get; set; }

    /// <summary>
    /// Expands environment variables and resolves the actual directory path.
    /// Creates the directory if it doesn't exist.
    /// </summary>
    /// <param name="configuredPath">The configured path (may contain environment variables).</param>
    /// <param name="defaultPath">The default path to use if configured path is null or empty.</param>
    /// <returns>The expanded and validated directory path.</returns>
    public static string ResolveDirectory(string? configuredPath, string defaultPath)
    {
        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? defaultPath
            : Environment.ExpandEnvironmentVariables(configuredPath);

        // Ensure directory exists
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        return path;
    }

    /// <summary>
    /// Gets the resolved log directory path.
    /// </summary>
    public string GetLogDirectory() =>
        ResolveDirectory(LogDirectory,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipMate", "Logs"));

    /// <summary>
    /// Gets the resolved temp directory path.
    /// </summary>
    public string GetTempDirectory() =>
        ResolveDirectory(TempDirectory,
            Path.Combine(Path.GetTempPath(), "ClipMate"));

    /// <summary>
    /// Gets the resolved templates directory path.
    /// </summary>
    public string GetTemplatesDirectory() =>
        ResolveDirectory(TemplatesDirectory,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClipMate", "Templates"));

    /// <summary>
    /// Gets the resolved export directory path.
    /// </summary>
    public string GetExportDirectory() =>
        ResolveDirectory(ExportDirectory,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ClipMate", "Exports"));

    /// <summary>
    /// Gets the resolved sounds directory path.
    /// Defaults to [AppDirectory]\Sounds if not configured.
    /// </summary>
    public string GetSoundsDirectory()
    {
        if (!string.IsNullOrWhiteSpace(SoundsDirectory))
            return ResolveDirectory(SoundsDirectory, string.Empty);

        // Default to application directory + Sounds
        var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(appDirectory, "Resources", "Sounds");
    }
}
