namespace ClipMate.Core.Models.Configuration;

/// <summary>
/// What the Text Cleanup Options dialog does with line breaks.
/// </summary>
public enum TextCleanupLineBreaks
{
    NoChange,

    /// <summary>
    /// Joins the lines of each paragraph and keeps the blank lines between paragraphs.
    /// </summary>
    WithinParagraphs,

    All,
}
