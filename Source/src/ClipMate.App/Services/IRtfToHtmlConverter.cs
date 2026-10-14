namespace ClipMate.App.Services;

/// <summary>
/// Converts Rich Text Format content to HTML.
/// </summary>
public interface IRtfToHtmlConverter
{
    /// <summary>
    /// Converts an RTF document to an HTML fragment.
    /// </summary>
    /// <param name="rtf">The RTF document text.</param>
    /// <returns>The HTML representation, or an empty string when the input is empty.</returns>
    string ConvertToHtml(string rtf);
}
