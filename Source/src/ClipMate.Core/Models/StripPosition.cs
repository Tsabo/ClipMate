namespace ClipMate.Core.Models;

/// <summary>
/// Defines the position from which to strip characters.
/// </summary>
public enum StripPosition
{
    /// <summary>
    /// Strip characters from the beginning of the text.
    /// </summary>
    Leading,

    /// <summary>
    /// Strip characters from the end of the text.
    /// </summary>
    Trailing,

    /// <summary>
    /// Strip characters from anywhere in the text.
    /// </summary>
    Anywhere
}
