namespace ClipMate.Core.Models;

/// <summary>
/// Represents an application filter rule to exclude clipboard captures from specific apps.
/// </summary>
public class ApplicationFilter
{
    /// <summary>
    /// Unique identifier for the filter rule.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Display name for the filter rule.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Process name to match (e.g., "notepad.exe"). Null to ignore process name.
    /// </summary>
    public string? ProcessName { get; set; }

    /// <summary>
    /// Window title pattern to match (supports wildcards). Null to ignore window title.
    /// </summary>
    public string? WindowTitlePattern { get; set; }

    /// <summary>
    /// Whether this filter rule is currently active.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Timestamp when the filter was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp of last modification.
    /// </summary>
    public DateTime? ModifiedAt { get; set; }
}
