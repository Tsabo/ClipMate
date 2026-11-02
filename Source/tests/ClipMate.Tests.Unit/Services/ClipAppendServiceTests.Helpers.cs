using ClipMate.Data.Services;

namespace ClipMate.Tests.Unit.Services;

public partial class ClipAppendServiceTests
{
    [Test]
    [Category("ProcessEscapeSequences")]
    [Arguments("Hello\\nWorld", "Hello\nWorld")]
    [Arguments("Hello\\tWorld", "Hello\tWorld")]
    [Arguments("Hello\\rWorld", "Hello\rWorld")]
    [Arguments("Line1\\nLine2\\tTabbed\\rReturn", "Line1\nLine2\tTabbed\rReturn")]
    [Arguments("", "")]
    public async Task ProcessEscapeSequences_TurnsEscapesIntoTheirCharacters(string separator, string expected)
    {
        // Act
        var result = ClipAppendService.ProcessEscapeSequences(separator);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    [Category("StripTrailingLineBreaks")]
    [Arguments("Hello World\n", "Hello World")]
    [Arguments("Hello World\r\n", "Hello World")]
    [Arguments("Hello World\n\n\n", "Hello World")]
    [Arguments("Hello World", "Hello World")]
    public async Task StripTrailingLineBreaks_RemovesLineBreaksAtTheEnd(string text, string expected)
    {
        // Act
        var result = ClipAppendService.StripTrailingLineBreaks(text);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }
}
