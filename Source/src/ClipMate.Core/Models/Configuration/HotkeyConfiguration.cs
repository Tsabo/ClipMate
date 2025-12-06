namespace ClipMate.Core.Models.Configuration;

/// <summary>
/// Represents application hotkey configuration.
/// </summary>
public class HotkeyConfiguration
{
    /// <summary>
    /// Gets or sets the hotkey to activate the main window (e.g., "Ctrl+Alt+C").
    /// </summary>
    public string Activate { get; set; } = "Ctrl+Alt+C";

    /// <summary>
    /// Gets or sets the hotkey to capture clipboard (e.g., "Win+C").
    /// </summary>
    public string Capture { get; set; } = "Win+C";

    /// <summary>
    /// Gets or sets the hotkey to toggle auto-capture (e.g., "Win+Shift+C").
    /// </summary>
    public string AutoCapture { get; set; } = "Win+Shift+C";

    /// <summary>
    /// Gets or sets the hotkey for quick paste/PowerPaste (e.g., "Shift+Ctrl+Q").
    /// </summary>
    public string QuickPaste { get; set; } = "Shift+Ctrl+Q";

    /// <summary>
    /// Gets or sets the hotkey for screen capture (e.g., "Ctrl+Alt+F12").
    /// </summary>
    public string ScreenCapture { get; set; } = "Ctrl+Alt+F12";

    /// <summary>
    /// Gets or sets the hotkey for screen object capture (e.g., "Ctrl+Alt+F11").
    /// </summary>
    public string ScreenCaptureObject { get; set; } = "Ctrl+Alt+F11";

    /// <summary>
    /// Gets or sets the hotkey to select next clip (e.g., "Ctrl+Alt+N").
    /// </summary>
    public string SelectNext { get; set; } = "Ctrl+Alt+N";

    /// <summary>
    /// Gets or sets the hotkey to select previous clip (e.g., "Ctrl+Alt+P").
    /// </summary>
    public string SelectPrevious { get; set; } = "Ctrl+Alt+P";

    /// <summary>
    /// Gets or sets the hotkey to view clip details (e.g., "Ctrl+Alt+F2").
    /// </summary>
    public string ViewClip { get; set; } = "Ctrl+Alt+F2";

    /// <summary>
    /// Gets or sets the hotkey for manual filter operations (e.g., "Win+W").
    /// </summary>
    public string ManualFilter { get; set; } = "Win+W";

    /// <summary>
    /// Gets or sets the hotkey for popup ClipBar list (e.g., "Ctrl+Shift+Alt+C").
    /// </summary>
    public string PopupClipBar { get; set; } = "Ctrl+Shift+Alt+C";
}
