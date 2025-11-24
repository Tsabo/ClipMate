using ClipMate.Core.Models;

namespace ClipMate.Tests.Unit.Services;

public partial class TextTransformServiceTests
{
    #region SortLines Tests

    [Test]
    public async Task SortLines_Alphabetically_ShouldSortCorrectly()
    {
        // Arrange
        var input = "Zebra\nApple\nBanana\nCherry";
        var expected = "Apple\nBanana\nCherry\nZebra";

        // Act
        var result = _service.SortLines(input, SortMode.Alphabetical);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task SortLines_Numerically_ShouldSortCorrectly()
    {
        // Arrange
        var input = "10\n2\n100\n21";
        var expected = "2\n10\n21\n100";

        // Act
        var result = _service.SortLines(input, SortMode.Numerical);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task SortLines_Reverse_ShouldReverseLines()
    {
        // Arrange
        var input = "Line 1\nLine 2\nLine 3";
        var expected = "Line 3\nLine 2\nLine 1";

        // Act
        var result = _service.SortLines(input, SortMode.Reverse);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task SortLines_WithEmptyLines_ShouldPreserveEmptyLines()
    {
        // Arrange
        var input = "Apple\n\nBanana\n\nCherry";
        var expected = "\n\nApple\nBanana\nCherry";

        // Act
        var result = _service.SortLines(input, SortMode.Alphabetical);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    #endregion
}
