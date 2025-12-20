namespace ClipMate.Tests.Unit.Services;

public partial class TextTransformServiceTests
{
    [Test]
    public async Task CleanUpText_RemoveExtraSpaces_ShouldCollapseSpaces()
    {
        // Arrange
        const string input = "Text  with   multiple    spaces";
        const string expected = "Text with multiple spaces";

        // Act
        var result = _service.CleanUpText(input, removeExtraSpaces: true);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task CleanUpText_RemoveExtraLineBreaks_ShouldCollapseBreaks()
    {
        // Arrange
        const string input = "Line 1\n\n\nLine 2\n\n\n\nLine 3";
        const string expected = "Line 1\n\nLine 2\n\nLine 3";

        // Act
        var result = _service.CleanUpText(input, removeExtraLineBreaks: true);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task CleanUpText_TrimLines_ShouldTrimEachLine()
    {
        // Arrange
        const string input = "  Line 1  \n  Line 2  \n  Line 3  ";
        const string expected = "Line 1\nLine 2\nLine 3";

        // Act
        var result = _service.CleanUpText(input, trimLines: true);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task CleanUpText_AllOptions_ShouldApplyAll()
    {
        // Arrange
        const string input = "  Line  1  \n\n\n  Line   2  ";
        const string expected = "Line 1\n\nLine 2";

        // Act
        var result = _service.CleanUpText(
            input, 
            removeExtraSpaces: true, 
            removeExtraLineBreaks: true, 
            trimLines: true);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }
}
