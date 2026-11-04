namespace ClipMate.Core.Models.Configuration;

/// <summary>
/// The resets on the Advanced options tab. Until ClipMate restarts, open windows and the Options dialog keep saving the
/// values they hold, so a reset is only recorded and is carried out when the settings are next loaded. ClipMate 7 solves
/// the same problem by no longer saving once a reset has run.
/// </summary>
public static class SettingsReset
{
    /// <summary>
    /// Records a reset for the next start. A wider reset already waiting is kept.
    /// </summary>
    public static void Request(ClipMateConfiguration configuration, PendingSettingsReset reset)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (reset > configuration.PendingSettingsReset)
            configuration.PendingSettingsReset = reset;
    }

    /// <summary>
    /// Carries out the waiting reset, if any.
    /// </summary>
    /// <returns>The settings to use, with no reset waiting.</returns>
    public static ClipMateConfiguration ApplyPending(ClipMateConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        switch (configuration.PendingSettingsReset)
        {
            case PendingSettingsReset.LayoutAndFonts:
                ResetLayoutAndFonts(configuration);
                configuration.PendingSettingsReset = PendingSettingsReset.None;
                return configuration;

            case PendingSettingsReset.Everything:
                return ResetEverything(configuration);

            case PendingSettingsReset.None:
            default:
                return configuration;
        }
    }

    private static void ResetLayoutAndFonts(ClipMateConfiguration configuration)
    {
        var preferences = configuration.Preferences;
        var defaults = new PreferencesConfiguration();

        preferences.ClassicWindowHotkey = defaults.ClassicWindowHotkey;
        preferences.ClassicWindowTaskbar = defaults.ClassicWindowTaskbar;
        preferences.ClassicExpandedHeight = defaults.ClassicExpandedHeight;
        preferences.ClassicIsDroppedDown = defaults.ClassicIsDroppedDown;
        preferences.ClassicIsTacked = defaults.ClassicIsTacked;
        preferences.ExplorerWindow = defaults.ExplorerWindow;
        preferences.ClipBarLastPosition = defaults.ClipBarLastPosition;

        var editorDefaults = new MonacoEditorConfiguration();
        configuration.MonacoEditor.FontFamily = editorDefaults.FontFamily;
        configuration.MonacoEditor.FontSize = editorDefaults.FontSize;
    }

    /// <summary>
    /// Starts from a new configuration, so a section added later is reset without being listed here. ClipMate 7 erases
    /// its whole registry key, Database list included. Here, what the user has built up rather than chosen stays:
    /// without the Database list ClipMate would run setup again and appear to have lost the clips, and a pairing can't
    /// be restored from this Device alone.
    /// </summary>
    private static ClipMateConfiguration ResetEverything(ClipMateConfiguration configuration) =>
        new()
        {
            Version = ConfigurationUpgrader.CurrentVersion,
            Databases = configuration.Databases,
            DefaultDatabase = configuration.DefaultDatabase,
            SavedSearchQueries = configuration.SavedSearchQueries,
            Peers = configuration.Peers,
        };
}
