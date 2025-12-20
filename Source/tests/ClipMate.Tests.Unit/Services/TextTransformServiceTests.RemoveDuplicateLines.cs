namespace ClipMate.Tests.Unit.Services;

public partial class TextTransformServiceTests
{
    [Test]
    public async Task RemoveDuplicateLines_WithDuplicates_ShouldRemoveThem()
    {
        // Arrange
        const string input = "Apple\nBanana\nApple\nCherry\nBanana";
        const string expected = "Apple\nBanana\nCherry";

        // Act
        var result = _service.RemoveDuplicateLines(input);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task RemoveDuplicateLines_CaseInsensitive_ShouldRemoveDuplicates()
    {
        // Arrange
        const string input = "Apple\napple\nAPPLE\nBanana";
        const string expected = "Apple\nBanana";

        // Act
        var result = _service.RemoveDuplicateLines(input, caseSensitive: false);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task RemoveDuplicateLines_CaseSensitive_ShouldKeepDifferentCase()
    {
        // Arrange
        const string input = "Apple\napple\nAPPLE\nBanana";
        const string expected = "Apple\napple\nAPPLE\nBanana";

        // Act
        var result = _service.RemoveDuplicateLines(input, caseSensitive: true);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task RemoveDuplicateLines_WithEmptyLines_ShouldKeepOneEmptyLine()
    {
        // Arrange
        const string input = "Apple\n\nBanana\n\nCherry\n";
        const string expected = "Apple\n\nBanana\nCherry";

        // Act
        var result = _service.RemoveDuplicateLines(input);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }
}
