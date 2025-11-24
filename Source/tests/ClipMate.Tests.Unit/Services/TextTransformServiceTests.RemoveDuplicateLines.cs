namespace ClipMate.Tests.Unit.Services;

public partial class TextTransformServiceTests
{
    #region RemoveDuplicateLines Tests

    [Test]
    public async Task RemoveDuplicateLines_WithDuplicates_ShouldRemoveThem()
    {
        // Arrange
        var input = "Apple\nBanana\nApple\nCherry\nBanana";
        var expected = "Apple\nBanana\nCherry";

        // Act
        var result = _service.RemoveDuplicateLines(input);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task RemoveDuplicateLines_CaseInsensitive_ShouldRemoveDuplicates()
    {
        // Arrange
        var input = "Apple\napple\nAPPLE\nBanana";
        var expected = "Apple\nBanana";

        // Act
        var result = _service.RemoveDuplicateLines(input, caseSensitive: false);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task RemoveDuplicateLines_CaseSensitive_ShouldKeepDifferentCase()
    {
        // Arrange
        var input = "Apple\napple\nAPPLE\nBanana";
        var expected = "Apple\napple\nAPPLE\nBanana";

        // Act
        var result = _service.RemoveDuplicateLines(input, caseSensitive: true);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task RemoveDuplicateLines_WithEmptyLines_ShouldKeepOneEmptyLine()
    {
        // Arrange
        var input = "Apple\n\nBanana\n\nCherry\n";
        var expected = "Apple\n\nBanana\nCherry";

        // Act
        var result = _service.RemoveDuplicateLines(input);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    #endregion
}
