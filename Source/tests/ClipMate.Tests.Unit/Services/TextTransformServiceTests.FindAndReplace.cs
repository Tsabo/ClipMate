namespace ClipMate.Tests.Unit.Services;

public partial class TextTransformServiceTests
{
    #region FindAndReplace Tests

    [Test]
    public async Task FindAndReplace_Literal_ShouldReplaceAll()
    {
        // Arrange
        var input = "The quick brown fox jumps over the lazy dog";
        var expected = "The fast brown fox jumps over the lazy dog";

        // Act
        var result = _service.FindAndReplace(input, "quick", "fast", isRegex: false);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task FindAndReplace_LiteralCaseInsensitive_ShouldReplaceAll()
    {
        // Arrange
        var input = "Apple apple APPLE";
        var expected = "Orange Orange Orange";

        // Act
        var result = _service.FindAndReplace(input, "apple", "Orange", isRegex: false, caseSensitive: false);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task FindAndReplace_Regex_ShouldReplacePattern()
    {
        // Arrange
        var input = "Contact: john@example.com or jane@example.com";
        var expected = "Contact: [EMAIL] or [EMAIL]";

        // Act
        var result = _service.FindAndReplace(input, @"\S+@\S+\.\S+", "[EMAIL]", isRegex: true);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task FindAndReplace_RegexWithGroups_ShouldReplaceWithCapture()
    {
        // Arrange
        var input = "Name: John Doe, Age: 30";
        var expected = "Name: Doe, John, Age: 30";

        // Act
        var result = _service.FindAndReplace(
            input, 
            @"(\w+) (\w+)", 
            "$2, $1", 
            isRegex: true);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task FindAndReplace_InvalidRegex_ShouldThrowArgumentException()
    {
        // Arrange
        var input = "Test text";

        // Act & Assert
        await Assert.That(() => 
            _service.FindAndReplace(input, "[invalid(regex", "replacement", isRegex: true)).Throws<ArgumentException>();
    }

    #endregion
}
