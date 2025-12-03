using ClipMate.Core.Models;

namespace ClipMate.Core.Services;

/// <summary>
/// Service interface for text transformation and manipulation operations.
/// </summary>
public interface ITextTransformService
{
    /// <summary>
    /// Converts text to the specified case format.
    /// </summary>
    string ConvertCase(string text, CaseConversion conversion);

    /// <summary>
    /// Sorts lines in the text according to the specified mode.
    /// </summary>
    string SortLines(string text, SortMode mode);

    /// <summary>
    /// Removes duplicate lines from the text.
    /// </summary>
    string RemoveDuplicateLines(string text, bool caseSensitive = false);

    /// <summary>
    /// Adds line numbers to each line in the text.
    /// </summary>
    string AddLineNumbers(string text, string format = "{0}. ", int startNumber = 1);

    /// <summary>
    /// Finds and replaces text with support for regex patterns.
    /// </summary>
    string FindAndReplace(string text, string find, string replace, bool useRegex = false, bool caseSensitive = false);

    /// <summary>
    /// Performs cleanup operations on text.
    /// </summary>
    string CleanUpText(string text, bool removeExtraSpaces = false, bool removeExtraLineBreaks = false, bool trimLines = false);

    /// <summary>
    /// Converts text between different formats.
    /// </summary>
    string ConvertFormat(string text, TextFormat sourceFormat, TextFormat targetFormat);

    /// <summary>
    /// Inverts the case of each character in the text.
    /// </summary>
    string InvertCase(string text);

    /// <summary>
    /// Removes line breaks from text according to the specified mode.
    /// </summary>
    string RemoveLineBreaks(string text, LineBreakMode mode);

    /// <summary>
    /// Trims leading and trailing whitespace from each line.
    /// </summary>
    string TrimText(string text);

    /// <summary>
    /// Strips specified characters from the text at the specified position.
    /// </summary>
    string StripCharacters(string text, string characters, StripPosition position);
}
