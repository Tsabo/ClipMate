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
    /// Gets or sets the full path to the database file.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

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
    /// Gets or sets the cleanup method for aging and purging.
    /// </summary>
    public CleanupMethod CleanupMethod { get; set; } = CleanupMethod.AfterHourIdle;

    /// <summary>
    /// Gets or sets the number of days to keep clips before purging.
    /// </summary>
    public int PurgeDays { get; set; } = 7;

    /// <summary>
    /// Gets or sets the username associated with this database.
    /// </summary>
    public string UserName { get; set; } = Environment.UserName;

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

    /// <summary>
    /// Gets or sets the date of the last database backup.
    /// </summary>
    public DateTime? LastBackupDate { get; set; }

    /// <summary>
    /// Gets or sets the date when the user was last prompted for a backup.
    /// Used to implement a snooze period before prompting again.
    /// </summary>
    public DateTime? LastBackupPromptDate { get; set; }

    /// <summary>
    /// Gets or sets the directory where database backups should be stored.
    /// </summary>
    public string BackupDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the temp file location for this database.
    /// </summary>
    public TempFileLocation TempFileLocation { get; set; } = TempFileLocation.DatabaseDirectory;

    /// <summary>
    /// Gets or sets the time when the database should be set offline daily.
    /// Null means the feature is disabled.
    /// </summary>
    public TimeSpan? SetOfflineDailyAt { get; set; }

    /// <summary>
    /// Gets or sets the remote user ID for authentication (future use for PostgreSQL/SQL Server).
    /// </summary>
    public string? RemoteUserId { get; set; }

    /// <summary>
    /// Gets or sets the remote password for authentication (future use for PostgreSQL/SQL Server).
    /// </summary>
    public string? RemotePassword { get; set; }
}
