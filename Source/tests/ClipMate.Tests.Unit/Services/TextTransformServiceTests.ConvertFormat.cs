using ClipMate.Core.Models;

namespace ClipMate.Tests.Unit.Services;

public partial class TextTransformServiceTests
{
    [Test]
    public async Task ConvertFormat_PlainToPlain_ShouldReturnSame()
    {
        // Arrange
        const string input = "Plain text";

        // Act
        var result = _service.ConvertFormat(input, TextFormat.Plain, TextFormat.Plain);

        // Assert
        await Assert.That(result).IsEqualTo(input);
    }

    [Test]
    public async Task ConvertFormat_PlainToHtml_ShouldWrapInParagraph()
    {
        // Arrange
        const string input = "Line 1\nLine 2";
        const string expected = "<p>Line 1<br/>Line 2</p>";

        // Act
        var result = _service.ConvertFormat(input, TextFormat.Plain, TextFormat.Html);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task ConvertFormat_HtmlToPlain_ShouldStripTags()
    {
        // Arrange
        const string input = "<p>Hello <strong>world</strong>!</p>";
        const string expected = "Hello world!";

        // Act
        var result = _service.ConvertFormat(input, TextFormat.Html, TextFormat.Plain);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }
}
