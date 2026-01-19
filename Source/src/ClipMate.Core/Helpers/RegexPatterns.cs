using System.Text.RegularExpressions;

namespace ClipMate.Core.Helpers;

/// <summary>
/// Centralized source-generated regex patterns used throughout the application.
/// Uses .NET 7+ GeneratedRegex attribute for improved performance.
/// </summary>
public static partial class RegexPatterns
{
    #region Data/Encryption Patterns

    /// <summary>
    /// Matches valid Base64 strings with optional padding (0-2 '=' characters).
    /// Used for validating encrypted data format.
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z0-9+/]*={0,2}$")]
    public static partial Regex IsBase64();

    #endregion

    #region Text Processing Patterns

    /// <summary>
    /// Matches trailing line breaks (CR, LF, or CRLF) at the end of text.
    /// Used for text normalization in append operations.
    /// </summary>
    [GeneratedRegex(@"(\r\n|\n|\r)+$", RegexOptions.Compiled)]
    public static partial Regex TrailingLineBreak();

    #endregion

    #region HTML Processing Patterns

    /// <summary>
    /// Extracts the SourceURL from HTML clipboard metadata comments.
    /// Captures: <!--SourceURL: https://example.com-->
    /// </summary>
    [GeneratedRegex(@"<!--SourceURL:\s*(.+?)-->")]
    public static partial Regex SourceUrl();

    /// <summary>
    /// Matches script tags including their content (case-insensitive, multiline).
    /// Used for sanitizing HTML content.
    /// </summary>
    [GeneratedRegex(@"<script[^>]*>.*?</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline, "en-US")]
    public static partial Regex ScriptTag();

    /// <summary>
    /// Matches javascript: protocol URLs.
    /// Used for sanitizing HTML content.
    /// </summary>
    [GeneratedRegex(@"javascript:\s*[^""'\s>]+", RegexOptions.IgnoreCase, "en-US")]
    public static partial Regex JavascriptUrl();

    /// <summary>
    /// Matches inline event handlers with quoted values (onclick="...", onload='...').
    /// Used for sanitizing HTML content.
    /// </summary>
    [GeneratedRegex(@"\s+on\w+\s*=\s*[""'].*?[""']", RegexOptions.IgnoreCase, "en-US")]
    public static partial Regex InlineEventHandlerQuoted();

    /// <summary>
    /// Matches inline event handlers with unquoted values (onclick=handler).
    /// Used for sanitizing HTML content.
    /// </summary>
    [GeneratedRegex(@"\s+on\w+\s*=\s*[^\s>]+", RegexOptions.IgnoreCase, "en-US")]
    public static partial Regex InlineEventHandlerUnquoted();

    /// <summary>
    /// Extracts EndHTML offset from HTML clipboard format header.
    /// </summary>
    [GeneratedRegex(@"EndHTML:(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    public static partial Regex EndHtml();

    /// <summary>
    /// Extracts StartHTML offset from HTML clipboard format header.
    /// </summary>
    [GeneratedRegex(@"StartHTML:(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    public static partial Regex StartHtml();

    /// <summary>
    /// Strips HTML tags from text.
    /// Used for converting HTML to plain text.
    /// </summary>
    [GeneratedRegex("<[^>]+>")]
    public static partial Regex HtmlTag();

    #endregion

    #region SQL/Monaco Editor Patterns

    /// <summary>
    /// Extracts the token from SQL syntax error messages.
    /// Captures: near "token_name"
    /// </summary>
    [GeneratedRegex(@"near\s+\""([^""]+)\""", RegexOptions.IgnoreCase, "en-US")]
    public static partial Regex SqlErrorToken();

    #endregion
}
