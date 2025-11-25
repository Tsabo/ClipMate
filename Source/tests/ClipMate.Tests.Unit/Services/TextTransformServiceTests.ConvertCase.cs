using ClipMate.Core.Models;

namespace ClipMate.Tests.Unit.Services;

public partial class TextTransformServiceTests
{
    #region ConvertCase Tests

    [Test]
    [Arguments("hello world", CaseConversion.Uppercase, "HELLO WORLD")]
    [Arguments("HELLO WORLD", CaseConversion.Lowercase, "hello world")]
    [Arguments("hello world", CaseConversion.TitleCase, "Hello World")]
    [Arguments("hello. world! how are you?", CaseConversion.SentenceCase, "Hello. World! How are you?")]
    public async Task ConvertCase_WithValidInput_ShouldConvertCorrectly(
        string input, CaseConversion conversion, string expected)
    {
        // Act
        var result = _service.ConvertCase(input, conversion);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task ConvertCase_WithEmptyString_ShouldReturnEmpty()
    {
        // Act
        var result = _service.ConvertCase(string.Empty, CaseConversion.Uppercase);

        // Assert
        await Assert.That(result).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task ConvertCase_WithNull_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => 
            _service.ConvertCase(null!, CaseConversion.Uppercase)).Throws<ArgumentNullException>();
    }

    #endregion
}
