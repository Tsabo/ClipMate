namespace ClipMate.Core.Models;

/// <summary>
/// Represents an application-specific profile that controls which clipboard formats are captured.
/// Profiles are auto-generated when ClipMate encounters a new application.
/// </summary>
public class ApplicationProfile
{
    /// <summary>
    /// Gets or sets the application name (normalized: uppercase, no .EXE extension).
    /// Example: "NOTEPAD", "CHROME", "DEVENV"
    /// </summary>
    public string ApplicationName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether clipboard capture is enabled for this application.
    /// When false, ALL formats from this application are ignored (short-circuit).
    /// When true, individual format settings are respected.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the format capture settings.
    /// Key: Format name (exact Windows format name, e.g., "TEXT", "HTML Format", "Rich Text Format")
    /// Value: True to capture, False to ignore
    /// </summary>
    public Dictionary<string, bool> Formats { get; set; } = new();
}
