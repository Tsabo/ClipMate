using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;

namespace ClipMate.Tests.Unit.Services;

public partial class MarkdownConversionServiceTests
{
    [Test]
    public async Task ConvertHtmlToMarkdown_EmptyInput_ReturnsEmpty()
    {
        // Act
        var result = _service.ConvertHtmlToMarkdown("   ", new MarkdownConversionOptions());

        // Assert
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task ConvertHtmlToMarkdown_NullInput_Throws()
    {
        // Act & Assert
        await Assert.That(() => _service.ConvertHtmlToMarkdown(null!, new MarkdownConversionOptions()))
            .Throws<ArgumentNullException>();
    }

    [Test]
    [Arguments(MarkdownFlavor.Default)]
    [Arguments(MarkdownFlavor.GitHub)]
    [Arguments(MarkdownFlavor.MultiMarkdown)]
    [Arguments(MarkdownFlavor.Pandoc)]
    public async Task ConvertHtmlToMarkdown_Bold_UsesDoubleAsterisks(MarkdownFlavor flavor)
    {
        // Arrange
        const string html = "<p>Hello <strong>world</strong></p>";

        // Act
        var result = _service.ConvertHtmlToMarkdown(html, new MarkdownConversionOptions(flavor));

        // Assert
        await Assert.That(result).IsEqualTo("Hello **world**");
    }

    [Test]
    public async Task ConvertHtmlToMarkdown_SlackFlavorBold_UsesSingleAsterisks()
    {
        // Arrange
        const string html = "<p>Hello <strong>world</strong></p>";

        // Act
        var result = _service.ConvertHtmlToMarkdown(html, new MarkdownConversionOptions(MarkdownFlavor.Slack));

        // Assert
        await Assert.That(result).IsEqualTo("Hello *world*");
    }

    [Test]
    public async Task ConvertHtmlToMarkdown_GitHubFlavorBlockFirstHtml_IsConverted()
    {
        // Arrange - ReverseMarkdown's own GitHub flavor returns raw HTML for input like this
        const string html = "<h2>Title</h2><p>Text<!-- note --></p><p><del>old</del></p>";

        // Act
        var result = _service.ConvertHtmlToMarkdown(html, new MarkdownConversionOptions(MarkdownFlavor.GitHub));

        // Assert
        await Assert.That(result).IsEqualTo($"## Title{Environment.NewLine}{Environment.NewLine}Text{Environment.NewLine}{Environment.NewLine}~~old~~");
    }

    [Test]
    public async Task ConvertHtmlToMarkdown_SlackFlavorTable_RendersCodeBlock()
    {
        // Arrange - Slack has no tables and ReverseMarkdown throws on them
        const string html = "<p>Data</p><table><tr><th>Name</th></tr><tr><td><b>A</b></td></tr></table>";

        // Act
        var result = _service.ConvertHtmlToMarkdown(html, new MarkdownConversionOptions(MarkdownFlavor.Slack));

        // Assert
        await Assert.That(result).Contains("```");
        await Assert.That(result).Contains("| Name |");
        await Assert.That(result).Contains("| **A** |");
    }

    [Test]
    public async Task ConvertHtmlToMarkdown_TelegramFlavor_EscapesListMarkers()
    {
        // Arrange
        const string html = "<ul><li>item</li></ul>";

        // Act
        var result = _service.ConvertHtmlToMarkdown(html, new MarkdownConversionOptions(MarkdownFlavor.Telegram));

        // Assert
        await Assert.That(result).IsEqualTo(@"\- item");
    }

    [Test]
    public async Task ConvertHtmlToMarkdown_GitHubFlavorTable_ProducesPipeTable()
    {
        // Arrange
        const string html = "<table><tr><th>Name</th><th>Value</th></tr><tr><td>A</td><td>1</td></tr></table>";

        // Act
        var result = _service.ConvertHtmlToMarkdown(html, new MarkdownConversionOptions(MarkdownFlavor.GitHub));

        // Assert
        await Assert.That(result).Contains("| Name | Value |");
        await Assert.That(result).Contains("| A | 1 |");
    }

    [Test]
    public async Task ConvertHtmlToMarkdown_Heading_ProducesAtxHeading()
    {
        // Arrange
        const string html = "<h2>Section</h2>";

        // Act
        var result = _service.ConvertHtmlToMarkdown(html, new MarkdownConversionOptions());

        // Assert
        await Assert.That(result).IsEqualTo("## Section");
    }

    [Test]
    public async Task ConvertHtmlToMarkdown_RemoveCommentsEnabled_DropsComments()
    {
        // Arrange
        const string html = "<p>Text<!-- secret --></p>";

        // Act
        var result = _service.ConvertHtmlToMarkdown(html, new MarkdownConversionOptions(RemoveComments: true));

        // Assert
        await Assert.That(result).DoesNotContain("secret");
    }

    [Test]
    public async Task ConvertHtmlToMarkdown_ScriptsAndStyles_AreRemoved()
    {
        // Arrange
        const string html = "<style>p { color: red; }</style><p>Visible</p><script>alert('x');</script>";

        // Act
        var result = _service.ConvertHtmlToMarkdown(html, new MarkdownConversionOptions());

        // Assert
        await Assert.That(result).IsEqualTo("Visible");
    }

    [Test]
    public async Task ConvertHtmlToMarkdown_InlineBoldStyle_ConvertedToBold()
    {
        // Arrange - RTF exports and Office HTML express bold via inline styles
        const string html = "<p><span style=\"font-weight: bold\">Important</span></p>";

        // Act
        var result = _service.ConvertHtmlToMarkdown(html, new MarkdownConversionOptions(MarkdownFlavor.GitHub));

        // Assert
        await Assert.That(result).IsEqualTo("**Important**");
    }

    [Test]
    public async Task ConvertHtmlToMarkdown_SmartHrefEnabled_EmitsBareUrl()
    {
        // Arrange
        const string html = "<a href=\"https://example.com\">https://example.com</a>";

        // Act
        var result = _service.ConvertHtmlToMarkdown(html, new MarkdownConversionOptions(SmartHref: true));

        // Assert
        await Assert.That(result).IsEqualTo("https://example.com");
    }

    [Test]
    public async Task ConvertHtmlToMarkdown_Link_ProducesMarkdownLink()
    {
        // Arrange
        const string html = "<a href=\"https://example.com\">Example</a>";

        // Act
        var result = _service.ConvertHtmlToMarkdown(html, new MarkdownConversionOptions());

        // Assert
        await Assert.That(result).IsEqualTo("[Example](https://example.com)");
    }
}
