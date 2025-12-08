namespace ClipMate.Core.Models.Configuration;

/// <summary>
/// Represents the saved state and position of a window.
/// </summary>
public class WindowStateConfiguration
{
    /// <summary>
    /// Gets or sets the window's left position (X coordinate).
    /// Null indicates the window should use default positioning.
    /// </summary>
    public double? Left { get; set; }

    /// <summary>
    /// Gets or sets the window's top position (Y coordinate).
    /// Null indicates the window should use default positioning.
    /// </summary>
    public double? Top { get; set; }

    /// <summary>
    /// Gets or sets the window's width.
    /// Null indicates the window should use default width.
    /// </summary>
    public double? Width { get; set; }

    /// <summary>
    /// Gets or sets the window's height.
    /// Null indicates the window should use default height.
    /// </summary>
    public double? Height { get; set; }

    /// <summary>
    /// Gets or sets the window state (Normal, Minimized, Maximized).
    /// Default is Normal.
    /// </summary>
    public string WindowState { get; set; } = "Normal";
}
