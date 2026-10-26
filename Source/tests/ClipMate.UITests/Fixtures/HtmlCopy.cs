using System.Text;
using System.Windows;

namespace ClipMate.UITests.Fixtures;

/// <summary>
/// A copy as a browser makes it: HTML (CF_HTML) plus its plain text.
/// </summary>
public static class HtmlCopy
{
    /// <summary>
    /// A copy offering <paramref name="fragment" /> as CF_HTML, with <paramref name="text" /> as its plain text.
    /// </summary>
    public static DataObject Create(string fragment, string text)
    {
        const string header = "Version:0.9\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";
        const string before = "<html><body><!--StartFragment-->";
        const string after = "<!--EndFragment--></body></html>";
        var headerLength = string.Format(header, 0, 0, 0, 0).Length;
        var startFragment = headerLength + Encoding.UTF8.GetByteCount(before);
        var endFragment = startFragment + Encoding.UTF8.GetByteCount(fragment);
        var endHtml = endFragment + Encoding.UTF8.GetByteCount(after);

        var data = new DataObject();
        data.SetData(DataFormats.Html, string.Format(header, headerLength, endHtml, startFragment, endFragment) + before + fragment + after);
        data.SetText(text);
        return data;
    }
}
