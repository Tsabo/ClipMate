namespace ClipMate.Tests.Unit.Services;

public partial class TextTransformServiceTests
{
    #region AddLineNumbers Tests

    [Test]
    public async Task AddLineNumbers_WithDefaultFormat_ShouldAddNumbers()
    {
        // Arrange
        var input = "Line one\nLine two\nLine three";
        var expected = "1. Line one\n2. Line two\n3. Line three";

        // Act
        var result = _service.AddLineNumbers(input);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task AddLineNumbers_WithCustomFormat_ShouldUseFormat()
    {
        // Arrange
        var input = "Line one\nLine two";
        var expected = "[001] Line one\n[002] Line two";

        // Act
        var result = _service.AddLineNumbers(input, format: "[{0:D3}] ");

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task AddLineNumbers_WithStartNumber_ShouldStartFromNumber()
    {
        // Arrange
        var input = "Line one\nLine two";
        var expected = "10. Line one\n11. Line two";

        // Act
        var result = _service.AddLineNumbers(input, startNumber: 10);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    #endregion
}
