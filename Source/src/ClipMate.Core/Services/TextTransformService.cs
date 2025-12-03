using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClipMate.Core.Models;

namespace ClipMate.Core.Services;

/// <summary>
/// Service for text transformation and manipulation operations.
/// User Story 6: Text Processing Tools
/// </summary>
public class TextTransformService : ITextTransformService
{
    private static readonly TextInfo _textInfo = CultureInfo.CurrentCulture.TextInfo;

    /// <summary>
    /// Converts text to the specified case format.
    /// </summary>
    /// <param name="text">The text to convert.</param>
    /// <param name="conversion">The case conversion type.</param>
    /// <returns>The converted text.</returns>
    /// <exception cref="ArgumentNullException">Thrown when text is null.</exception>
    public string ConvertCase(string text, CaseConversion conversion)
    {
        if (text == null)
        {
            throw new ArgumentNullException(nameof(text));
        }
        
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        return conversion switch
        {
            CaseConversion.Uppercase => text.ToUpper(),
            CaseConversion.Lowercase => text.ToLower(),
            CaseConversion.TitleCase => _textInfo.ToTitleCase(text.ToLower()),
            CaseConversion.SentenceCase => ConvertToSentenceCase(text),
            CaseConversion.InvertCase => InvertCase(text),
            _ => text
        };
    }

    /// <summary>
    /// Sorts lines in the text according to the specified mode.
    /// </summary>
    /// <param name="text">The text containing lines to sort.</param>
    /// <param name="mode">The sorting mode.</param>
    /// <returns>The text with sorted lines.</returns>
    public string SortLines(string text, SortMode mode)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        var lines = text.Split('\n');

        var sorted = mode switch
        {
            SortMode.Alphabetical => lines.OrderBy(l => l, StringComparer.Ordinal).ToArray(),
            SortMode.Numerical => lines.OrderBy(l => 
            {
                var trimmed = l.Trim();
                return int.TryParse(trimmed, out var num) ? num : int.MaxValue;
            }).ToArray(),
            SortMode.Reverse => lines.AsEnumerable().Reverse().ToArray(),
            _ => lines
        };

        return string.Join('\n', sorted);
    }

    /// <summary>
    /// Removes duplicate lines from the text.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <param name="caseSensitive">Whether to perform case-sensitive comparison.</param>
    /// <returns>The text with duplicate lines removed.</returns>
    public string RemoveDuplicateLines(string text, bool caseSensitive = false)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        var lines = text.Split('\n');
        var comparer = caseSensitive 
            ? StringComparer.Ordinal 
            : StringComparer.OrdinalIgnoreCase;

        var seen = new HashSet<string>(comparer);
        var result = new List<string>();

        foreach (var line in lines)
        {
            if (seen.Add(line))
            {
                result.Add(line);
            }
        }

        return string.Join('\n', result);
    }

    /// <summary>
    /// Adds line numbers to each line in the text.
    /// </summary>
    /// <param name="text">The text to add line numbers to.</param>
    /// <param name="format">The format string for line numbers (default: "{0}. ").</param>
    /// <param name="startNumber">The starting number (default: 1).</param>
    /// <returns>The text with line numbers added.</returns>
    public string AddLineNumbers(string text, string format = "{0}. ", int startNumber = 1)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        var lines = text.Split('\n');
        var result = new StringBuilder();

        for (int i = 0; i < lines.Length; i++)
        {
            result.Append(string.Format(format, startNumber + i));
            result.Append(lines[i]);
            
            if (i < lines.Length - 1)
            {
                result.Append('\n');
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Finds and replaces text using literal string matching or regex patterns.
    /// </summary>
    /// <param name="text">The text to search in.</param>
    /// <param name="find">The text or pattern to find.</param>
    /// <param name="replace">The replacement text.</param>
    /// <param name="isRegex">Whether to use regex pattern matching.</param>
    /// <param name="caseSensitive">Whether to perform case-sensitive search.</param>
    /// <returns>The text with replacements applied.</returns>
    /// <exception cref="ArgumentException">Thrown when regex pattern is invalid.</exception>
    public string FindAndReplace(
        string text, 
        string find, 
        string replace, 
        bool isRegex = false, 
        bool caseSensitive = true)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }
        
        if (string.IsNullOrEmpty(find))
        {
            return text;
        }

        if (isRegex)
        {
            try
            {
                var options = caseSensitive 
                    ? RegexOptions.None 
                    : RegexOptions.IgnoreCase;

                return Regex.Replace(text, find, replace, options);
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException($"Invalid regex pattern: {find}", nameof(find), ex);
            }
        }

        var comparison = caseSensitive 
            ? StringComparison.Ordinal 
            : StringComparison.OrdinalIgnoreCase;

        return text.Replace(find, replace, comparison);
    }

    /// <summary>
    /// Cleans up text by removing extra spaces, line breaks, and trimming lines.
    /// </summary>
    /// <param name="text">The text to clean up.</param>
    /// <param name="removeExtraSpaces">Whether to collapse multiple spaces.</param>
    /// <param name="removeExtraLineBreaks">Whether to collapse multiple line breaks.</param>
    /// <param name="trimLines">Whether to trim whitespace from each line.</param>
    /// <returns>The cleaned text.</returns>
    public string CleanUpText(
        string text, 
        bool removeExtraSpaces = false, 
        bool removeExtraLineBreaks = false, 
        bool trimLines = false)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        var result = text;

        if (removeExtraSpaces)
        {
            result = Regex.Replace(result, @" {2,}", " ");
        }

        if (removeExtraLineBreaks)
        {
            result = Regex.Replace(result, @"\n{3,}", "\n\n");
        }

        if (trimLines)
        {
            var lines = result.Split('\n');
            result = string.Join('\n', lines.Select(l => l.Trim()));
        }

        return result;
    }

    /// <summary>
    /// Converts text between different formats (Plain, RTF, HTML).
    /// </summary>
    /// <param name="text">The text to convert.</param>
    /// <param name="sourceFormat">The source format.</param>
    /// <param name="targetFormat">The target format.</param>
    /// <returns>The converted text.</returns>
    public string ConvertFormat(string text, TextFormat sourceFormat, TextFormat targetFormat)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }
        
        if (sourceFormat == targetFormat)
        {
            return text;
        }

        // Plain to HTML
        if (sourceFormat == TextFormat.Plain && targetFormat == TextFormat.Html)
        {
            var escaped = text.Replace("<", "&lt;").Replace(">", "&gt;");
            var withBreaks = escaped.Replace("\n", "<br/>");
            return $"<p>{withBreaks}</p>";
        }

        // HTML to Plain
        if (sourceFormat == TextFormat.Html && targetFormat == TextFormat.Plain)
        {
            // Simple HTML tag removal (for basic cases)
            var result = Regex.Replace(text, @"<[^>]+>", string.Empty);
            result = result.Replace("&lt;", "<").Replace("&gt;", ">")
                          .Replace("&amp;", "&").Replace("&quot;", "\"");
            return result;
        }

        // For other conversions, return as-is for now
        // Full RTF conversion would require more complex logic
        return text;
    }

    /// <summary>
    /// Inverts the case of each character in the text.
    /// </summary>
    /// <param name="text">The text to invert.</param>
    /// <returns>The text with inverted case.</returns>
    public string InvertCase(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        var result = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (char.IsUpper(c))
            {
                result.Append(char.ToLower(c));
            }
            else if (char.IsLower(c))
            {
                result.Append(char.ToUpper(c));
            }
            else
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Removes line breaks from text according to the specified mode.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <param name="mode">The line break removal mode.</param>
    /// <returns>The text with line breaks removed.</returns>
    public string RemoveLineBreaks(string text, LineBreakMode mode)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        return mode switch
        {
            LineBreakMode.PreserveParagraphs => Regex.Replace(text, @"(?<!\n)\n(?!\n)", " "),
            LineBreakMode.RemoveAll => text.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " "),
            LineBreakMode.UrlCrunch => RemoveUrlLineBreaks(text),
            _ => text
        };
    }

    /// <summary>
    /// Trims leading and trailing whitespace from each line.
    /// </summary>
    /// <param name="text">The text to trim.</param>
    /// <returns>The trimmed text.</returns>
    public string TrimText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        var lines = text.Split('\n');
        var trimmed = lines.Select(l => l.Trim());
        return string.Join('\n', trimmed);
    }

    /// <summary>
    /// Strips specified characters from the text at the specified position.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <param name="characters">The characters to strip.</param>
    /// <param name="position">The position from which to strip characters.</param>
    /// <returns>The text with characters stripped.</returns>
    public string StripCharacters(string text, string characters, StripPosition position)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(characters))
        {
            return text ?? string.Empty;
        }

        return position switch
        {
            StripPosition.Leading => text.TrimStart(characters.ToCharArray()),
            StripPosition.Trailing => text.TrimEnd(characters.ToCharArray()),
            StripPosition.Anywhere => StripCharactersAnywhere(text, characters),
            _ => text
        };
    }

    #region Private Helper Methods

    private static string ConvertToSentenceCase(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var result = new StringBuilder();
        bool capitalizeNext = true;

        foreach (char c in text)
        {
            if (capitalizeNext && char.IsLetter(c))
            {
                result.Append(char.ToUpper(c));
                capitalizeNext = false;
            }
            else
            {
                result.Append(char.ToLower(c));
            }

            // Capitalize after sentence-ending punctuation
            if (c == '.' || c == '!' || c == '?')
            {
                capitalizeNext = true;
            }
        }

        return result.ToString();
    }

    private static string RemoveUrlLineBreaks(string text)
    {
        // Pattern to detect URLs that have been broken across lines
        // Look for http(s):// or www. followed by characters that shouldn't have line breaks
        var urlPattern = @"(https?://[^\s]+|www\.[^\s]+)";
        
        var result = new StringBuilder();
        var currentLine = new StringBuilder();
        
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            currentLine.Append(trimmed);
            
            // Check if this looks like it might be in the middle of a URL
            var currentText = currentLine.ToString();
            if (Regex.IsMatch(currentText, urlPattern) && 
                !currentText.Contains(' ') && 
                !trimmed.EndsWith('.') &&
                !trimmed.EndsWith('!') &&
                !trimmed.EndsWith('?'))
            {
                // Likely in the middle of a URL, don't add line break
                continue;
            }
            
            // Complete line or not a URL
            result.Append(currentLine);
            result.Append('\n');
            currentLine.Clear();
        }
        
        // Add any remaining content
        if (currentLine.Length > 0)
        {
            result.Append(currentLine);
        }
        
        return result.ToString().TrimEnd('\n');
    }

    private static string StripCharactersAnywhere(string text, string characters)
    {
        var charSet = new HashSet<char>(characters);
        var result = new StringBuilder(text.Length);
        
        foreach (char c in text)
        {
            if (!charSet.Contains(c))
            {
                result.Append(c);
            }
        }
        
        return result.ToString();
    }

    #endregion
}
