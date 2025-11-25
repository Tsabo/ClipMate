namespace ClipMate.Tests.Unit.Services;

public partial class TextTransformServiceTests
{
    #region CleanUpText Tests

    [Test]
    public async Task CleanUpText_RemoveExtraSpaces_ShouldCollapseSpaces()
    {
        // Arrange
        var input = "Text  with   multiple    spaces";
        var expected = "Text with multiple spaces";

        // Act
        var result = _service.CleanUpText(input, removeExtraSpaces: true);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task CleanUpText_RemoveExtraLineBreaks_ShouldCollapseBreaks()
    {
        // Arrange
        var input = "Line 1\n\n\nLine 2\n\n\n\nLine 3";
        var expected = "Line 1\n\nLine 2\n\nLine 3";

        // Act
        var result = _service.CleanUpText(input, removeExtraLineBreaks: true);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task CleanUpText_TrimLines_ShouldTrimEachLine()
    {
        // Arrange
        var input = "  Line 1  \n  Line 2  \n  Line 3  ";
        var expected = "Line 1\nLine 2\nLine 3";

        // Act
        var result = _service.CleanUpText(input, trimLines: true);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task CleanUpText_AllOptions_ShouldApplyAll()
    {
        // Arrange
        var input = "  Line  1  \n\n\n  Line   2  ";
        var expected = "Line 1\n\nLine 2";

        // Act
        var result = _service.CleanUpText(
            input, 
            removeExtraSpaces: true, 
            removeExtraLineBreaks: true, 
            trimLines: true);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    #endregion
}
