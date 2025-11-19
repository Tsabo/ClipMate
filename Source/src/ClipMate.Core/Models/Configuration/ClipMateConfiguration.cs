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
}

/// <summary>
/// General application preferences.
/// </summary>
public class PreferencesConfiguration
{
    /// <summary>
    /// Gets or sets whether auto-capture is enabled at startup.
    /// </summary>
    public bool AutoCaptureAtStartup { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to hide the taskbar icon.
    /// </summary>
    public bool HideTaskbarIcon { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to show system tray icon.
    /// </summary>
    public bool ShowSystemTrayIcon { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to capture existing clipboard on startup.
    /// </summary>
    public bool CaptureExistingClipboard { get; set; } = true;

    /// <summary>
    /// Gets or sets the delay (in milliseconds) after copy operation.
    /// </summary>
    public int DelayAfterCopy { get; set; } = 999;

    /// <summary>
    /// Gets or sets the delay (in milliseconds) on clipboard update.
    /// </summary>
    public int DelayOnClipboardUpdate { get; set; } = 250;

    /// <summary>
    /// Gets or sets whether to beep on clipboard update.
    /// </summary>
    public bool BeepOnUpdate { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to beep on append.
    /// </summary>
    public bool BeepOnAppend { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to beep on erase.
    /// </summary>
    public bool BeepOnErase { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to beep on filter.
    /// </summary>
    public bool BeepOnFilter { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to beep on ignore.
    /// </summary>
    public bool BeepOnIgnore { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to show hints/tooltips.
    /// </summary>
    public bool ShowHint { get; set; } = true;

    /// <summary>
    /// Gets or sets the hint hide pause duration (in milliseconds).
    /// </summary>
    public int HintHidePause { get; set; } = 4500;

    /// <summary>
    /// Gets or sets the language/locale.
    /// </summary>
    public string Language { get; set; } = "English";

    /// <summary>
    /// Gets or sets the logging level (0=None, 1=Error, 2=Warning, 3=Info, 4=Debug, 5=Verbose).
    /// </summary>
    public int LogLevel { get; set; } = 3;

    /// <summary>
    /// Gets or sets whether the application has been registered (licensed).
    /// </summary>
    public bool IsRegistered { get; set; } = false;

    /// <summary>
    /// Gets or sets the PowerPaste delay (in milliseconds).
    /// </summary>
    public int PowerPasteDelay { get; set; } = 100;

    /// <summary>
    /// Gets or sets whether PowerPaste shield is enabled (prevents accidental activation).
    /// </summary>
    public bool PowerPasteShield { get; set; } = true;

    /// <summary>
    /// Gets or sets the PowerPaste delimiter characters.
    /// </summary>
    public string PowerPasteDelimiter { get; set; } = ",.;:\\n\\t";

    /// <summary>
    /// Gets or sets whether to trim items during PowerPaste.
    /// </summary>
    public bool PowerPasteTrim { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include delimiter in PowerPaste.
    /// </summary>
    public bool PowerPasteIncludeDelimiter { get; set; } = false;

    /// <summary>
    /// Gets or sets whether PowerPaste should loop.
    /// </summary>
    public bool PowerPasteLoop { get; set; } = false;

    /// <summary>
    /// Gets or sets whether PowerPaste should explode (split) items.
    /// </summary>
    public bool PowerPasteExplode { get; set; } = false;
}
