namespace ClipMate.Core.Models.Configuration;

/// <summary>
/// Configuration settings for the Monaco Editor component.
/// </summary>
public class MonacoEditorConfiguration
{
    /// <summary>
    /// Gets or sets the editor theme (vs, vs-dark, hc-black).
    /// </summary>
    public string Theme { get; set; } = "vs-light";

    /// <summary>
    /// Gets or sets the font size in pixels.
    /// </summary>
    public int FontSize { get; set; } = 14;

    /// <summary>
    /// Gets or sets the font family.
    /// </summary>
    public string FontFamily { get; set; } = "Consolas";

    /// <summary>
    /// Gets or sets whether word wrap is enabled.
    /// </summary>
    public bool WordWrap { get; set; } = true;

    /// <summary>
    /// Gets or sets whether line numbers are shown.
    /// </summary>
    public bool ShowLineNumbers { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the minimap is shown.
    /// </summary>
    public bool ShowMinimap { get; set; } = false;

    /// <summary>
    /// Gets or sets the number of spaces for a tab character.
    /// </summary>
    public int TabSize { get; set; } = 4;

    /// <summary>
    /// Gets or sets whether smooth scrolling is enabled.
    /// </summary>
    public bool SmoothScrolling { get; set; } = true;

    /// <summary>
    /// Gets or sets whether word and character counts are displayed.
    /// </summary>
    public bool DisplayWordAndCharacterCounts { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the toolbar is shown in the editor.
    /// </summary>
    public bool ShowToolbar { get; set; } = true;

    /// <summary>
    /// Gets or sets whether debug mode is enabled (opens DevTools for Monaco editor).
    /// </summary>
    public bool EnableDebug { get; set; } = false;
}
