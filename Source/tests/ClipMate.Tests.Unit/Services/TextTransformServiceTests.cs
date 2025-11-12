using ClipMate.Core.Models;
using ClipMate.Core.Services;
using Shouldly;
using Xunit;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Unit tests for TextTransformService text manipulation functionality.
/// User Story 6: Text Processing Tools
/// </summary>
public class TextTransformServiceTests
{
    private readonly TextTransformService _service;

    public TextTransformServiceTests()
    {
        _service = new TextTransformService();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldCreateInstance()
    {
        // Arrange & Act
        var service = new TextTransformService();

        // Assert
        service.ShouldNotBeNull();
    }

    #endregion

    #region ConvertCase Tests

    [Theory]
    [InlineData("hello world", CaseConversion.Uppercase, "HELLO WORLD")]
    [InlineData("HELLO WORLD", CaseConversion.Lowercase, "hello world")]
    [InlineData("hello world", CaseConversion.TitleCase, "Hello World")]
    [InlineData("hello. world! how are you?", CaseConversion.SentenceCase, "Hello. World! How are you?")]
    public void ConvertCase_WithValidInput_ShouldConvertCorrectly(
        string input, CaseConversion conversion, string expected)
    {
        // Act
        var result = _service.ConvertCase(input, conversion);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void ConvertCase_WithEmptyString_ShouldReturnEmpty()
    {
        // Act
        var result = _service.ConvertCase(string.Empty, CaseConversion.Uppercase);

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void ConvertCase_WithNull_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => 
            _service.ConvertCase(null!, CaseConversion.Uppercase));
    }

    #endregion

    #region SortLines Tests

    [Fact]
    public void SortLines_Alphabetically_ShouldSortCorrectly()
    {
        // Arrange
        var input = "Zebra\nApple\nBanana\nCherry";
        var expected = "Apple\nBanana\nCherry\nZebra";

        // Act
        var result = _service.SortLines(input, SortMode.Alphabetical);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void SortLines_Numerically_ShouldSortCorrectly()
    {
        // Arrange
        var input = "10\n2\n100\n21";
        var expected = "2\n10\n21\n100";

        // Act
        var result = _service.SortLines(input, SortMode.Numerical);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void SortLines_Reverse_ShouldReverseLines()
    {
        // Arrange
        var input = "Line 1\nLine 2\nLine 3";
        var expected = "Line 3\nLine 2\nLine 1";

        // Act
        var result = _service.SortLines(input, SortMode.Reverse);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void SortLines_WithEmptyLines_ShouldPreserveEmptyLines()
    {
        // Arrange
        var input = "Apple\n\nBanana\n\nCherry";
        var expected = "\n\nApple\nBanana\nCherry";

        // Act
        var result = _service.SortLines(input, SortMode.Alphabetical);

        // Assert
        result.ShouldBe(expected);
    }

    #endregion

    #region RemoveDuplicateLines Tests

    [Fact]
    public void RemoveDuplicateLines_WithDuplicates_ShouldRemoveThem()
    {
        // Arrange
        var input = "Apple\nBanana\nApple\nCherry\nBanana";
        var expected = "Apple\nBanana\nCherry";

        // Act
        var result = _service.RemoveDuplicateLines(input);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void RemoveDuplicateLines_CaseInsensitive_ShouldRemoveDuplicates()
    {
        // Arrange
        var input = "Apple\napple\nAPPLE\nBanana";
        var expected = "Apple\nBanana";

        // Act
        var result = _service.RemoveDuplicateLines(input, caseSensitive: false);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void RemoveDuplicateLines_CaseSensitive_ShouldKeepDifferentCase()
    {
        // Arrange
        var input = "Apple\napple\nAPPLE\nBanana";
        var expected = "Apple\napple\nAPPLE\nBanana";

        // Act
        var result = _service.RemoveDuplicateLines(input, caseSensitive: true);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void RemoveDuplicateLines_WithEmptyLines_ShouldKeepOneEmptyLine()
    {
        // Arrange
        var input = "Apple\n\nBanana\n\nCherry\n";
        var expected = "Apple\n\nBanana\nCherry";

        // Act
        var result = _service.RemoveDuplicateLines(input);

        // Assert
        result.ShouldBe(expected);
    }

    #endregion

    #region AddLineNumbers Tests

    [Fact]
    public void AddLineNumbers_WithDefaultFormat_ShouldAddNumbers()
    {
        // Arrange
        var input = "Line one\nLine two\nLine three";
        var expected = "1. Line one\n2. Line two\n3. Line three";

        // Act
        var result = _service.AddLineNumbers(input);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void AddLineNumbers_WithCustomFormat_ShouldUseFormat()
    {
        // Arrange
        var input = "Line one\nLine two";
        var expected = "[001] Line one\n[002] Line two";

        // Act
        var result = _service.AddLineNumbers(input, format: "[{0:D3}] ");

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void AddLineNumbers_WithStartNumber_ShouldStartFromNumber()
    {
        // Arrange
        var input = "Line one\nLine two";
        var expected = "10. Line one\n11. Line two";

        // Act
        var result = _service.AddLineNumbers(input, startNumber: 10);

        // Assert
        result.ShouldBe(expected);
    }

    #endregion

    #region FindAndReplace Tests

    [Fact]
    public void FindAndReplace_Literal_ShouldReplaceAll()
    {
        // Arrange
        var input = "The quick brown fox jumps over the lazy dog";
        var expected = "The fast brown fox jumps over the lazy dog";

        // Act
        var result = _service.FindAndReplace(input, "quick", "fast", isRegex: false);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void FindAndReplace_LiteralCaseInsensitive_ShouldReplaceAll()
    {
        // Arrange
        var input = "Apple apple APPLE";
        var expected = "Orange Orange Orange";

        // Act
        var result = _service.FindAndReplace(input, "apple", "Orange", isRegex: false, caseSensitive: false);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void FindAndReplace_Regex_ShouldReplacePattern()
    {
        // Arrange
        var input = "Contact: john@example.com or jane@example.com";
        var expected = "Contact: [EMAIL] or [EMAIL]";

        // Act
        var result = _service.FindAndReplace(input, @"\S+@\S+\.\S+", "[EMAIL]", isRegex: true);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void FindAndReplace_RegexWithGroups_ShouldReplaceWithCapture()
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
        result.ShouldBe(expected);
    }

    [Fact]
    public void FindAndReplace_InvalidRegex_ShouldThrowArgumentException()
    {
        // Arrange
        var input = "Test text";

        // Act & Assert
        Should.Throw<ArgumentException>(() => 
            _service.FindAndReplace(input, "[invalid(regex", "replacement", isRegex: true));
    }

    #endregion

    #region CleanUpText Tests

    [Fact]
    public void CleanUpText_RemoveExtraSpaces_ShouldCollapseSpaces()
    {
        // Arrange
        var input = "Text  with   multiple    spaces";
        var expected = "Text with multiple spaces";

        // Act
        var result = _service.CleanUpText(input, removeExtraSpaces: true);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void CleanUpText_RemoveExtraLineBreaks_ShouldCollapseBreaks()
    {
        // Arrange
        var input = "Line 1\n\n\nLine 2\n\n\n\nLine 3";
        var expected = "Line 1\n\nLine 2\n\nLine 3";

        // Act
        var result = _service.CleanUpText(input, removeExtraLineBreaks: true);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void CleanUpText_TrimLines_ShouldTrimEachLine()
    {
        // Arrange
        var input = "  Line 1  \n  Line 2  \n  Line 3  ";
        var expected = "Line 1\nLine 2\nLine 3";

        // Act
        var result = _service.CleanUpText(input, trimLines: true);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void CleanUpText_AllOptions_ShouldApplyAll()
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
        result.ShouldBe(expected);
    }

    #endregion

    #region ConvertFormat Tests

    [Fact]
    public void ConvertFormat_PlainToPlain_ShouldReturnSame()
    {
        // Arrange
        var input = "Plain text";

        // Act
        var result = _service.ConvertFormat(input, TextFormat.Plain, TextFormat.Plain);

        // Assert
        result.ShouldBe(input);
    }

    [Fact]
    public void ConvertFormat_PlainToHtml_ShouldWrapInParagraph()
    {
        // Arrange
        var input = "Line 1\nLine 2";
        var expected = "<p>Line 1<br/>Line 2</p>";

        // Act
        var result = _service.ConvertFormat(input, TextFormat.Plain, TextFormat.Html);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void ConvertFormat_HtmlToPlain_ShouldStripTags()
    {
        // Arrange
        var input = "<p>Hello <strong>world</strong>!</p>";
        var expected = "Hello world!";

        // Act
        var result = _service.ConvertFormat(input, TextFormat.Html, TextFormat.Plain);

        // Assert
        result.ShouldBe(expected);
    }

    #endregion
}
