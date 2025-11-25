namespace ClipMate.Core.Models.Configuration;

/// <summary>
/// Represents an application-specific clipboard format filter profile.
/// Used to control which clipboard formats are captured from specific applications.
/// </summary>
public class ApplicationProfile
{
    /// <summary>
    /// Gets or sets the application name/process name (e.g., "DEVENV", "CHROME").
    /// </summary>
    public string ApplicationName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this profile is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the clipboard formats and their capture settings.
    /// Key is format name (e.g., "TEXT", "HTML Format", "Rich Text Format")
    /// Value is whether to capture (1) or ignore (0) this format.
    /// </summary>
    public Dictionary<string, int> Formats { get; set; } = new();
}
