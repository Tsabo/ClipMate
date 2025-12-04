namespace ClipMate.Core.Models;

/// <summary>
/// Defines the mode for removing line breaks from text.
/// </summary>
public enum LineBreakMode
{
    /// <summary>
    /// Remove single line breaks but preserve paragraph breaks (double line breaks).
    /// </summary>
    PreserveParagraphs,

    /// <summary>
    /// Remove all line breaks, joining all text into one line.
    /// </summary>
    RemoveAll,

    /// <summary>
    /// Remove line breaks that interrupt URLs.
    /// </summary>
    UrlCrunch
}
