namespace ClipMate.Core.Models.Configuration;

/// <summary>
/// Brings settings saved by an older ClipMate up to <see cref="CurrentVersion" />. Each step runs once: the
/// <see cref="ClipMateConfiguration.Version" /> saved with the settings records which steps already ran, so a change
/// the user makes afterwards (deleting an entry a step added, say) is never undone.
/// </summary>
public static class ConfigurationUpgrader
{
    /// <summary>
    /// The version this ClipMate writes. New settings start at it.
    /// </summary>
    public const int CurrentVersion = 4;

    private const string _ctfmonBadTarget = "CTFMON:";
    private const string _oldToggleAutoCaptureDefault = "Ctrl+Alt+A";

    /// <summary>
    /// Runs the steps <paramref name="configuration" /> has not had yet and sets its version to
    /// <see cref="CurrentVersion" />.
    /// </summary>
    /// <returns>True when anything changed, so the settings should be saved.</returns>
    public static bool Upgrade(ClipMateConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration.Version >= CurrentVersion)
            return false;

        if (configuration.Version < 2)
            AddCtfmonToBadTargets(configuration.Preferences);

        if (configuration.Version < 3)
            MoveCaptureDelayToDelayOnClipboardUpdate(configuration.Preferences);

        if (configuration.Version < 4)
            ClearOldToggleAutoCaptureDefault(configuration.Hotkeys);

        configuration.Version = CurrentVersion;

        return true;
    }

    // Version 2: CTFMON: (the text input framework) joined ClipMate 7's default BAD target list
    private static void AddCtfmonToBadTargets(PreferencesConfiguration preferences)
    {
        if (!preferences.QuickPasteBadTargets.Contains(_ctfmonBadTarget, StringComparer.OrdinalIgnoreCase))
            preferences.QuickPasteBadTargets = [.. preferences.QuickPasteBadTargets, _ctfmonBadTarget];
    }

    // Version 3: the capture delay became ClipMate 7's delay on clipboard update; the old settle time is dropped
    private static void MoveCaptureDelayToDelayOnClipboardUpdate(PreferencesConfiguration preferences)
    {
        if (preferences.LegacyCaptureDelayMs is { } captureDelayMs)
            preferences.DelayOnClipboardUpdateMs = captureDelayMs;

        preferences.LegacyCaptureDelayMs = null;
    }

    // Version 4: Toggle Auto-Capture lost its default, Ctrl+Alt+A, which took Append's shortcut and turned capture off
    private static void ClearOldToggleAutoCaptureDefault(HotkeyConfiguration hotkeys)
    {
        if (string.Equals(hotkeys.AutoCapture, _oldToggleAutoCaptureDefault, StringComparison.OrdinalIgnoreCase))
            hotkeys.AutoCapture = string.Empty;
    }
}
