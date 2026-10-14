using ClipMate.App.Services;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Unit tests for DevExpressRtfToHtmlConverter.
/// </summary>
public class DevExpressRtfToHtmlConverterTests
{
    private readonly DevExpressRtfToHtmlConverter _converter = new();

    [Test]
    public async Task ConvertToHtml_EmptyInput_ReturnsEmpty()
    {
        // Act
        var result = _converter.ConvertToHtml(string.Empty);

        // Assert
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task ConvertToHtml_BoldRtf_ProducesBoldHtml()
    {
        // Arrange
        const string rtf = @"{\rtf1\ansi\deff0 {\fonttbl {\f0 Arial;}} Plain {\b Strong} text\par}";

        // Act
        var result = _converter.ConvertToHtml(rtf);

        // Assert
        await Assert.That(result).Contains("Strong");
        await Assert.That(result).Matches("(?is)(<b>|<strong>|font-weight:\\s*bold).*Strong");
    }
}
