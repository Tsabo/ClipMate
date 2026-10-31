namespace ClipMate.Core.Services.Abstractions;

/// <summary>
/// Service interface for converting HTML content to Markdown.
/// </summary>
public interface IMarkdownConversionService
{
    /// <summary>
    /// Converts an HTML fragment or document to Markdown in the requested flavor.
    /// </summary>
    /// <param name="html">The HTML to convert (not CF_HTML; strip the clipboard header first).</param>
    /// <param name="options">Conversion options.</param>
    /// <returns>The Markdown text, or an empty string when the input is empty.</returns>
    string ConvertHtmlToMarkdown(string html, MarkdownConversionOptions options);
}
