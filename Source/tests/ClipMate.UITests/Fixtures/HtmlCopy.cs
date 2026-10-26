using System.Text;
using System.Windows;

namespace ClipMate.UITests.Fixtures;

/// <summary>
/// A copy as a browser makes it: HTML (CF_HTML) plus its plain text.
/// </summary>
public static class HtmlCopy
{
    /// <summary>
    /// A copy offering <paramref name="fragment" /> as CF_HTML, with <paramref name="text" /> as its plain text and,
    /// when given, <paramref name="sourceUrl" /> as the page it was copied from.
    /// </summary>
    public static DataObject Create(string fragment, string text, string? sourceUrl = null)
    {
        var header = "Version:0.9\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n" +
                     (sourceUrl != null ? $"SourceURL:{sourceUrl}\r\n" : "");
        const string before = "<html><body><!--StartFragment-->";
        const string after = "<!--EndFragment--></body></html>";
        var headerLength = Encoding.UTF8.GetByteCount(string.Format(header, 0, 0, 0, 0));
        var startFragment = headerLength + Encoding.UTF8.GetByteCount(before);
        var endFragment = startFragment + Encoding.UTF8.GetByteCount(fragment);
        var endHtml = endFragment + Encoding.UTF8.GetByteCount(after);

        var data = new DataObject();
        data.SetData(DataFormats.Html, string.Format(header, headerLength, endHtml, startFragment, endFragment) + before + fragment + after);
        data.SetText(text);
        return data;
    }
}
