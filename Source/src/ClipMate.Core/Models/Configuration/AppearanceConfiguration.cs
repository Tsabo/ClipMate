namespace ClipMate.Core.Models.Configuration;

/// <summary>
/// Configuration settings for application appearance/theming.
/// </summary>
public class AppearanceConfiguration
{
    /// <summary>
    /// Gets or sets the application-wide UI theme.
    /// </summary>
    public AppTheme AppTheme { get; set; } = AppTheme.Light;

    /// <summary>
    /// Gets or sets whether the Monaco editor theme automatically follows <see cref="AppTheme" />.
    /// When true, the Monaco editor theme is kept in sync with the app theme and the manual
    /// editor theme picker is disabled.
    /// </summary>
    public bool MonacoThemeFollowsAppTheme { get; set; } = true;
}
