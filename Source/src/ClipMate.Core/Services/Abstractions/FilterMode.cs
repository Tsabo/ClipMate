namespace ClipMate.Core.Services.Abstractions;

/// <summary>
/// Whether a write keeps only the plain text, or the image when there is no text.
/// </summary>
public enum FilterMode
{
    /// <summary>
    /// As the Outbound Filter setting says.
    /// </summary>
    FromSettings,

    /// <summary>
    /// Filtered whatever the setting, as Manual Filter does; a clip from an excluded Source Application is still left
    /// whole.
    /// </summary>
    Forced,
}
