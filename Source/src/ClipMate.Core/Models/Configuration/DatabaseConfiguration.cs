namespace ClipMate.Core.Models.Configuration;

/// <summary>
/// Represents a database configuration for ClipMate.
/// </summary>
public class DatabaseConfiguration
{
    /// <summary>
    /// Gets or sets the database name (display name).
    /// </summary>
    public string Name { get; set; } = "My Clips";

    /// <summary>
    /// Gets or sets the directory where the database file is stored.
    /// </summary>
    public string Directory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this database should auto-load on startup.
    /// </summary>
    public bool AutoLoad { get; set; } = true;

    /// <summary>
    /// Gets or sets whether this database allows backups.
    /// </summary>
    public bool AllowBackup { get; set; } = true;

    /// <summary>
    /// Gets or sets whether this database is read-only.
    /// </summary>
    public bool ReadOnly { get; set; } = false;

    /// <summary>
    /// Gets or sets the cleanup method (0=None, 1=Manual, 2=OnExit, 3=Daily, 4=Weekly).
    /// </summary>
    public int CleanupMethod { get; set; } = 3;

    /// <summary>
    /// Gets or sets the number of days to keep clips before purging.
    /// </summary>
    public int PurgeDays { get; set; } = 7;

    /// <summary>
    /// Gets or sets the username associated with this database.
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this is a remote database.
    /// </summary>
    public bool IsRemote { get; set; } = false;

    /// <summary>
    /// Gets or sets whether this database supports multi-user access.
    /// </summary>
    public bool MultiUser { get; set; } = false;

    /// <summary>
    /// Gets or sets the remote host address (if remote).
    /// </summary>
    public string? RemoteHost { get; set; }

    /// <summary>
    /// Gets or sets the remote database name (if remote).
    /// </summary>
    public string? RemoteDatabase { get; set; }

    /// <summary>
    /// Gets or sets whether this is a temporary/command-line database.
    /// </summary>
    public bool IsCommandLineDatabase { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to use modification timestamps.
    /// </summary>
    public bool UseModificationTimeStamp { get; set; } = true;
}
