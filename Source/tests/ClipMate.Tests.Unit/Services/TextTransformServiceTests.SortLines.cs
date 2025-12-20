using ClipMate.Core.Models;

namespace ClipMate.Tests.Unit.Services;

public partial class TextTransformServiceTests
{
    [Test]
    public async Task SortLines_Alphabetically_ShouldSortCorrectly()
    {
        // Arrange
        const string input = "Zebra\nApple\nBanana\nCherry";
        const string expected = "Apple\nBanana\nCherry\nZebra";

        // Act
        var result = _service.SortLines(input, SortMode.Alphabetical);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task SortLines_Numerically_ShouldSortCorrectly()
    {
        // Arrange
        const string input = "10\n2\n100\n21";
        const string expected = "2\n10\n21\n100";

        // Act
        var result = _service.SortLines(input, SortMode.Numerical);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task SortLines_Reverse_ShouldReverseLines()
    {
        // Arrange
        const string input = "Line 1\nLine 2\nLine 3";
        const string expected = "Line 3\nLine 2\nLine 1";

        // Act
        var result = _service.SortLines(input, SortMode.Reverse);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task SortLines_WithEmptyLines_ShouldPreserveEmptyLines()
    {
        // Arrange
        const string input = "Apple\n\nBanana\n\nCherry";
        const string expected = "\n\nApple\nBanana\nCherry";

        // Act
        var result = _service.SortLines(input, SortMode.Alphabetical);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }
}
