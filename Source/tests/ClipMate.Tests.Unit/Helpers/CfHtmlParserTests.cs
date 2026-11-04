using System.Text;
using ClipMate.Core.Helpers;

namespace ClipMate.Tests.Unit.Helpers;

/// <summary>
/// Tests for CF_HTML clipboard format parsing.
/// </summary>
public class CfHtmlParserTests
{
    private const string _fragment = "<b>Café</b> text";

    /// <summary>
    /// Builds a spec-compliant CF_HTML string with UTF-8 byte offsets.
    /// </summary>
    private static string BuildCfHtml(string fragment, bool includeMarkers = true)
    {
        const string headerTemplate = "Version:0.9\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";
        var prefix = includeMarkers ? "<html><body>\r\n<!--StartFragment-->" : "<html><body>\r\n";
        var suffix = includeMarkers ? "<!--EndFragment-->\r\n</body></html>" : "\r\n</body></html>";

        var headerLength = Encoding.UTF8.GetByteCount(string.Format(headerTemplate, 0, 0, 0, 0));
        var startHtml = headerLength;
        var startFragment = startHtml + Encoding.UTF8.GetByteCount(prefix);
        var endFragment = startFragment + Encoding.UTF8.GetByteCount(fragment);
        var endHtml = endFragment + Encoding.UTF8.GetByteCount(suffix);

        return string.Format(headerTemplate, startHtml, endHtml, startFragment, endFragment) + prefix + fragment + suffix;
    }

    [Test]
    public async Task ExtractFragment_WithFragmentMarkers_ReturnsFragmentOnly()
    {
        // Arrange
        var cfHtml = BuildCfHtml(_fragment);

        // Act
        var result = CfHtmlParser.ExtractFragment(cfHtml);

        // Assert
        await Assert.That(result).IsEqualTo(_fragment);
    }

    [Test]
    public async Task ExtractFragment_WithoutMarkers_UsesUtf8ByteOffsets()
    {
        // Arrange - non-ASCII content makes byte offsets differ from char offsets
        var cfHtml = BuildCfHtml(_fragment, includeMarkers: false);

        // Act
        var result = CfHtmlParser.ExtractFragment(cfHtml);

        // Assert
        await Assert.That(result).IsEqualTo(_fragment);
    }

    [Test]
    public async Task ExtractFragment_PlainHtml_ReturnsInput()
    {
        // Arrange
        const string html = "<p>Plain</p>";

        // Act
        var result = CfHtmlParser.ExtractFragment(html);

        // Assert
        await Assert.That(result).IsEqualTo(html);
    }

    [Test]
    public async Task ExtractFragment_Empty_ReturnsEmpty()
    {
        // Act
        var result = CfHtmlParser.ExtractFragment(" ");

        // Assert
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task ParseCfHtmlFormat_WithHeader_ReturnsFullDocument()
    {
        // Arrange - ASCII only, since ParseCfHtmlFormat treats offsets as character positions
        var cfHtml = BuildCfHtml("<b>Hi</b>");

        // Act
        var result = CfHtmlParser.ParseCfHtmlFormat(cfHtml);

        // Assert
        await Assert.That(result).IsEqualTo("<html><body>\r\n<!--StartFragment--><b>Hi</b><!--EndFragment-->\r\n</body></html>");
    }

    [Test]
    public async Task ParseCfHtmlFormat_WithoutOffsets_FallsBackToFirstHtmlLine()
    {
        // Arrange
        const string cfHtml = "Version:0.9\r\nSourceURL:https://example.com\r\n<p>Body</p>";

        // Act
        var result = CfHtmlParser.ParseCfHtmlFormat(cfHtml);

        // Assert
        await Assert.That(result).IsEqualTo("<p>Body</p>");
    }

    [Test]
    public async Task StripScripts_RemovesScriptsAndHandlers()
    {
        // Arrange
        const string html = "<p onclick=\"evil()\">Hi</p><script>bad()</script><a href=\"javascript:bad()\">x</a>";

        // Act
        var result = CfHtmlParser.StripScripts(html);

        // Assert
        await Assert.That(result).IsEqualTo("<p>Hi</p><a href=\"#\">x</a>");
    }

    [Test]
    public async Task ExtractSourceUrl_WithHeader_ReturnsUrl()
    {
        // Arrange
        const string cfHtml = "Version:0.9\r\nStartHTML:0000000105\r\nEndHTML:0000000130\r\n" +
                              "SourceURL:https://example.com/page\r\n<html><body>x</body></html>";

        // Act
        var result = CfHtmlParser.ExtractSourceUrl(cfHtml);

        // Assert
        await Assert.That(result).IsEqualTo("https://example.com/page");
    }

    [Test]
    public async Task ExtractSourceUrl_WithHeaderInOtherCaseAndSpaces_ReturnsTrimmedUrl()
    {
        // Act
        var result = CfHtmlParser.ExtractSourceUrl("Version:1.0\nsourceurl:  https://example.com/page  \n<p>x</p>");

        // Assert
        await Assert.That(result).IsEqualTo("https://example.com/page");
    }

    [Test]
    [Arguments("Version:0.9\r\nStartHTML:0000000025\r\n<p>SourceURL:https://example.com</p>")]
    [Arguments("Version:0.9\r\n<p>x</p>\r\nSourceURL:https://example.com\r\n")]
    [Arguments("Version:0.9\r\nSourceURL:\r\n<p>x</p>")]
    [Arguments("SourceURL:https://example.com\r\n<p>x</p>")]
    [Arguments("<!--SourceURL: https://example.com--><p>x</p>")]
    [Arguments("")]
    public async Task ExtractSourceUrl_WithoutUrlInTheHeader_ReturnsNull(string html)
    {
        // Act
        var result = CfHtmlParser.ExtractSourceUrl(html);

        // Assert
        await Assert.That(result).IsNull();
    }
}
