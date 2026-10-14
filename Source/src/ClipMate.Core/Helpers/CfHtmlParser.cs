using System.Text;

namespace ClipMate.Core.Helpers;

/// <summary>
/// Helpers for working with the Windows CF_HTML clipboard format ("HTML Format").
/// CF_HTML prefixes the HTML with a metadata header such as "Version:0.9 StartHTML:... EndHTML:...".
/// </summary>
public static class CfHtmlParser
{
    private const string _startFragmentMarker = "<!--StartFragment-->";
    private const string _endFragmentMarker = "<!--EndFragment-->";

    /// <summary>
    /// Parses CF_HTML clipboard format to extract the full HTML document.
    /// Uses StartHTML/EndHTML offsets to preserve full document structure like ClipMate 7.5.
    /// Returns the input unchanged when it has no CF_HTML header.
    /// </summary>
    public static string ParseCfHtmlFormat(string cfHtml)
    {
        if (string.IsNullOrWhiteSpace(cfHtml))
            return string.Empty;

        // Check if this is CF_HTML format (has Version: header)
        if (!cfHtml.StartsWith("Version:", StringComparison.OrdinalIgnoreCase))
            return cfHtml; // Already plain HTML

        try
        {
            // Parse the StartHTML and EndHTML offsets to get the full HTML document
            // (This preserves <html> and <body> tags with their styles, like ClipMate 7.5)
            var startHtmlMatch = RegexPatterns.StartHtml().Match(cfHtml);
            var endHtmlMatch = RegexPatterns.EndHtml().Match(cfHtml);

            if (startHtmlMatch.Success && endHtmlMatch.Success)
            {
                var startHtml = int.Parse(startHtmlMatch.Groups[1].ValueSpan, null);
                var endHtml = int.Parse(endHtmlMatch.Groups[1].ValueSpan, null);

                // Extract the full HTML document between StartHTML and EndHTML
                if (startHtml >= 0 && endHtml > startHtml && endHtml <= cfHtml.Length)
                    return cfHtml.AsSpan(startHtml, endHtml - startHtml).Trim().ToString();
            }

            // Fallback: Find first line starting with '<' using span
            var htmlSpan = cfHtml.AsSpan();
            var lineStart = 0;

            while (lineStart < htmlSpan.Length)
            {
                // Find end of current line
                var lineEnd = htmlSpan[lineStart..].IndexOfAny('\r', '\n');
                if (lineEnd == -1)
                    lineEnd = htmlSpan.Length - lineStart;

                var line = htmlSpan.Slice(lineStart, lineEnd).TrimStart();
                if (line.Length > 0 && line[0] == '<')
                {
                    // Found HTML start - return from here to end
                    return htmlSpan[lineStart..].Trim().ToString();
                }

                // Move to next line
                lineStart += lineEnd;
                while (lineStart < htmlSpan.Length && (htmlSpan[lineStart] == '\r' || htmlSpan[lineStart] == '\n'))
                    lineStart++;
            }

            return cfHtml; // Return as-is if parsing fails
        }
        catch (Exception)
        {
            // If parsing fails, return the original content
            return cfHtml;
        }
    }

    /// <summary>
    /// Extracts only the copied fragment from CF_HTML content, without the surrounding
    /// &lt;html&gt;/&lt;head&gt;/&lt;body&gt; wrapper. Tries the &lt;!--StartFragment--&gt; markers first,
    /// then the StartFragment/EndFragment header offsets (UTF-8 byte offsets per the spec),
    /// and finally falls back to <see cref="ParseCfHtmlFormat" />.
    /// </summary>
    public static string ExtractFragment(string cfHtml)
    {
        if (string.IsNullOrWhiteSpace(cfHtml))
            return string.Empty;

        var startMarker = cfHtml.IndexOf(_startFragmentMarker, StringComparison.OrdinalIgnoreCase);
        if (startMarker >= 0)
        {
            var contentStart = startMarker + _startFragmentMarker.Length;
            var endMarker = cfHtml.IndexOf(_endFragmentMarker, contentStart, StringComparison.OrdinalIgnoreCase);
            if (endMarker > contentStart)
                return cfHtml.AsSpan(contentStart, endMarker - contentStart).Trim().ToString();
        }

        if (cfHtml.StartsWith("Version:", StringComparison.OrdinalIgnoreCase))
        {
            var startMatch = RegexPatterns.StartFragment().Match(cfHtml);
            var endMatch = RegexPatterns.EndFragment().Match(cfHtml);

            if (startMatch.Success && endMatch.Success &&
                int.TryParse(startMatch.Groups[1].ValueSpan, out var start) &&
                int.TryParse(endMatch.Groups[1].ValueSpan, out var end))
            {
                var bytes = Encoding.UTF8.GetBytes(cfHtml);
                if (start >= 0 && end > start && end <= bytes.Length)
                    return Encoding.UTF8.GetString(bytes, start, end - start).Trim();
            }
        }

        return ParseCfHtmlFormat(cfHtml);
    }

    /// <summary>
    /// Strips script tags and inline event handlers from HTML content for security.
    /// </summary>
    public static string StripScripts(string htmlContent)
    {
        if (string.IsNullOrWhiteSpace(htmlContent))
            return string.Empty;

        // Remove <script> tags and their content
        htmlContent = RegexPatterns.ScriptTag().Replace(htmlContent, "");

        // Remove inline event handlers (onclick, onload, onerror, etc.)
        htmlContent = RegexPatterns.InlineEventHandlerQuoted().Replace(htmlContent, "");
        htmlContent = RegexPatterns.InlineEventHandlerUnquoted().Replace(htmlContent, "");

        // Remove javascript: URLs
        htmlContent = RegexPatterns.JavascriptUrl().Replace(htmlContent, "#");

        return htmlContent;
    }

    /// <summary>
    /// Extracts the source URL from HTML content.
    /// Looks for &lt;!--SourceURL: http://example.com--&gt; comment pattern.
    /// </summary>
    public static string? ExtractSourceUrl(string htmlContent)
    {
        if (string.IsNullOrWhiteSpace(htmlContent))
            return null;

        var match = RegexPatterns.SourceUrl().Match(htmlContent);

        return match.Success
            ? match.Groups[1].Value.Trim()
            : null;
    }
}
