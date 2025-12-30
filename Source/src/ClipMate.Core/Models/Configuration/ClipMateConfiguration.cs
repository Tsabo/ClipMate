namespace ClipMate.Core.Models.Configuration;

/// <summary>
/// Root configuration for ClipMate application.
/// </summary>
public class ClipMateConfiguration
{
    /// <summary>
    /// Gets or sets the configuration file format version.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Gets or sets general application preferences.
    /// </summary>
    public PreferencesConfiguration Preferences { get; set; } = new();

    /// <summary>
    /// Gets or sets hotkey configuration.
    /// </summary>
    public HotkeyConfiguration Hotkeys { get; set; } = new();

    /// <summary>
    /// Gets or sets database configurations.
    /// Key is the database identifier, value is the database configuration.
    /// </summary>
    public Dictionary<string, DatabaseConfiguration> Databases { get; set; } = new();

    /// <summary>
    /// Gets or sets application-specific clipboard format profiles.
    /// Key is the application name, value is the profile configuration.
    /// </summary>
    public Dictionary<string, ApplicationProfile> ApplicationProfiles { get; set; } = new();

    /// <summary>
    /// Gets or sets the name of the default/active database.
    /// </summary>
    public string? DefaultDatabase { get; set; }

    /// <summary>
    /// Gets or sets the list of recently used emojis with usage statistics.
    /// </summary>
    public List<RecentEmoji> RecentEmojis { get; set; } = [];

    /// <summary>
    /// Gets or sets the Monaco Editor configuration settings.
    /// </summary>
    public MonacoEditorConfiguration MonacoEditor { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of saved search queries.
    /// These are stored in configuration and shared across all databases.
    /// </summary>
    public List<SavedSearchQuery> SavedSearchQueries { get; set; } = [];

    /// <summary>
    /// Gets or sets the directories configuration.
    /// </summary>
    public DirectoriesConfiguration Directories { get; set; } = new();

    /// <summary>
    /// Gets or sets the export/import configuration.
    /// </summary>
    public ExportConfiguration Export { get; set; } = new();
}
