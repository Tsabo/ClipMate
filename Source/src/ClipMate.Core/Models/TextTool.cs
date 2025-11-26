namespace ClipMate.Core.Models;

/// <summary>
///     Defines the types of text transformation tools available.
/// </summary>
public enum TextTool
{
    /// <summary>
    ///     Convert text case (uppercase, lowercase, title case, sentence case).
    /// </summary>
    ConvertCase,

    /// <summary>
    ///     Sort lines alphabetically, numerically, or in reverse.
    /// </summary>
    SortLines,

    /// <summary>
    ///     Remove duplicate lines from text.
    /// </summary>
    RemoveDuplicateLines,

    /// <summary>
    ///     Add line numbers to each line.
    /// </summary>
    AddLineNumbers,

    /// <summary>
    ///     Find and replace text (literal or regex).
    /// </summary>
    FindAndReplace,

    /// <summary>
    ///     Clean up text (remove extra spaces, line breaks, trim).
    /// </summary>
    CleanUpText,

    /// <summary>
    ///     Convert between text formats (Plain, RTF, HTML).
    /// </summary>
    ConvertFormat,
}
