using ClipMate.Core.Helpers;

namespace ClipMate.Tests.Unit.Helpers;

/// <summary>
/// Tests for the centralized RegexPatterns helper class.
/// Verifies all regex patterns match expected inputs and reject invalid inputs.
/// </summary>
public class RegexPatternsTests
{
    #region IsBase64 Tests

    [Test]
    public async Task IsBase64_WithValidBase64String_ReturnsTrue()
    {
        // Arrange
        var validBase64 = "SGVsbG8gV29ybGQ="; // "Hello World" in Base64

        // Act
        var result = RegexPatterns.IsBase64().IsMatch(validBase64);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsBase64_WithValidBase64NoPadding_ReturnsTrue()
    {
        // Arrange
        var validBase64 = "SGVsbG8gV29ybGQ"; // No padding

        // Act
        var result = RegexPatterns.IsBase64().IsMatch(validBase64);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsBase64_WithValidBase64SinglePadding_ReturnsTrue()
    {
        // Arrange
        var validBase64 = "SGVsbG8gV29ybGQh="; // Single padding

        // Act
        var result = RegexPatterns.IsBase64().IsMatch(validBase64);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsBase64_WithInvalidCharacters_ReturnsFalse()
    {
        // Arrange
        var invalidBase64 = "Hello@World!"; // Contains @ and !

        // Act
        var result = RegexPatterns.IsBase64().IsMatch(invalidBase64);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsBase64_WithTooMuchPadding_ReturnsFalse()
    {
        // Arrange
        var invalidBase64 = "SGVsbG8gV29ybGQ==="; // Three padding characters

        // Act
        var result = RegexPatterns.IsBase64().IsMatch(invalidBase64);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsBase64_WithEmptyString_ReturnsTrue()
    {
        // Arrange - Empty string is valid Base64
        var emptyString = string.Empty;

        // Act
        var result = RegexPatterns.IsBase64().IsMatch(emptyString);

        // Assert
        await Assert.That(result).IsTrue();
    }

    #endregion

    #region TrailingLineBreak Tests

    [Test]
    public async Task TrailingLineBreak_WithWindowsLineBreak_Matches()
    {
        // Arrange
        var text = "Hello World\r\n";

        // Act
        var result = RegexPatterns.TrailingLineBreak().IsMatch(text);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task TrailingLineBreak_WithUnixLineBreak_Matches()
    {
        // Arrange
        var text = "Hello World\n";

        // Act
        var result = RegexPatterns.TrailingLineBreak().IsMatch(text);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task TrailingLineBreak_WithMacLineBreak_Matches()
    {
        // Arrange
        var text = "Hello World\r";

        // Act
        var result = RegexPatterns.TrailingLineBreak().IsMatch(text);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task TrailingLineBreak_WithMultipleTrailingLineBreaks_Matches()
    {
        // Arrange
        var text = "Hello World\r\n\r\n\r\n";

        // Act
        var match = RegexPatterns.TrailingLineBreak().Match(text);

        // Assert
        await Assert.That(match.Success).IsTrue();
        await Assert.That(match.Value).IsEqualTo("\r\n\r\n\r\n");
    }

    [Test]
    public async Task TrailingLineBreak_WithNoTrailingLineBreak_DoesNotMatch()
    {
        // Arrange
        var text = "Hello World";

        // Act
        var result = RegexPatterns.TrailingLineBreak().IsMatch(text);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task TrailingLineBreak_WithMidLineBreak_DoesNotMatch()
    {
        // Arrange
        var text = "Hello\r\nWorld";

        // Act
        var result = RegexPatterns.TrailingLineBreak().IsMatch(text);

        // Assert
        await Assert.That(result).IsFalse();
    }

    #endregion

    #region SourceUrl Tests

    [Test]
    public async Task SourceUrl_WithValidSourceUrlComment_ExtractsUrl()
    {
        // Arrange
        var html = "<!--SourceURL: https://example.com-->";

        // Act
        var match = RegexPatterns.SourceUrl().Match(html);

        // Assert
        await Assert.That(match.Success).IsTrue();
        await Assert.That(match.Groups[1].Value).IsEqualTo("https://example.com");
    }

    [Test]
    public async Task SourceUrl_WithExtraSpacing_ExtractsUrl()
    {
        // Arrange
        var html = "<!--SourceURL:   https://example.com/page  -->";

        // Act
        var match = RegexPatterns.SourceUrl().Match(html);

        // Assert
        await Assert.That(match.Success).IsTrue();
        await Assert.That(match.Groups[1].Value).IsEqualTo("https://example.com/page  ");
    }

    [Test]
    public async Task SourceUrl_WithNoSourceUrl_DoesNotMatch()
    {
        // Arrange
        var html = "<!-- Just a comment -->";

        // Act
        var result = RegexPatterns.SourceUrl().IsMatch(html);

        // Assert
        await Assert.That(result).IsFalse();
    }

    #endregion

    #region ScriptTag Tests

    [Test]
    public async Task ScriptTag_WithBasicScriptTag_Matches()
    {
        // Arrange
        var html = "<script>alert('test');</script>";

        // Act
        var result = RegexPatterns.ScriptTag().IsMatch(html);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ScriptTag_WithScriptTagAttributes_Matches()
    {
        // Arrange
        var html = "<script type=\"text/javascript\" src=\"app.js\">console.log('loaded');</script>";

        // Act
        var match = RegexPatterns.ScriptTag().Match(html);

        // Assert
        await Assert.That(match.Success).IsTrue();
        await Assert.That(match.Value).IsEqualTo(html);
    }

    [Test]
    public async Task ScriptTag_WithMultilineScript_Matches()
    {
        // Arrange
        var html = @"<script>
function test() {
    console.log('multi-line');
}
</script>";

        // Act
        var result = RegexPatterns.ScriptTag().IsMatch(html);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ScriptTag_CaseInsensitive_Matches()
    {
        // Arrange
        var html = "<SCRIPT>alert('test');</SCRIPT>";

        // Act
        var result = RegexPatterns.ScriptTag().IsMatch(html);

        // Assert
        await Assert.That(result).IsTrue();
    }

    #endregion

    #region JavascriptUrl Tests

    [Test]
    public async Task JavascriptUrl_WithJavascriptProtocol_Matches()
    {
        // Arrange
        var html = "javascript:alert('xss')";

        // Act
        var result = RegexPatterns.JavascriptUrl().IsMatch(html);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task JavascriptUrl_WithSpacesAfterColon_Matches()
    {
        // Arrange
        var html = "javascript: void(0)";

        // Act
        var result = RegexPatterns.JavascriptUrl().IsMatch(html);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task JavascriptUrl_CaseInsensitive_Matches()
    {
        // Arrange
        var html = "JAVASCRIPT:alert('test')";

        // Act
        var result = RegexPatterns.JavascriptUrl().IsMatch(html);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task JavascriptUrl_WithNormalUrl_DoesNotMatch()
    {
        // Arrange
        var html = "https://example.com";

        // Act
        var result = RegexPatterns.JavascriptUrl().IsMatch(html);

        // Assert
        await Assert.That(result).IsFalse();
    }

    #endregion

    #region InlineEventHandler Tests

    [Test]
    public async Task InlineEventHandlerQuoted_WithDoubleQuotedHandler_Matches()
    {
        // Arrange
        var html = "<div onclick=\"alert('test')\">Click</div>";

        // Act
        var result = RegexPatterns.InlineEventHandlerQuoted().IsMatch(html);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task InlineEventHandlerQuoted_WithSingleQuotedHandler_Matches()
    {
        // Arrange
        var html = "<div onclick='alert(\"test\")'>Click</div>";

        // Act
        var result = RegexPatterns.InlineEventHandlerQuoted().IsMatch(html);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task InlineEventHandlerQuoted_WithOnloadEvent_Matches()
    {
        // Arrange
        var html = "<body onload=\"init()\">Content</body>";

        // Act
        var result = RegexPatterns.InlineEventHandlerQuoted().IsMatch(html);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task InlineEventHandlerUnquoted_WithUnquotedHandler_Matches()
    {
        // Arrange
        var html = "<div onclick=handleClick>Click</div>";

        // Act
        var result = RegexPatterns.InlineEventHandlerUnquoted().IsMatch(html);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task InlineEventHandlerQuoted_CaseInsensitive_Matches()
    {
        // Arrange
        var html = "<div ONCLICK=\"alert('test')\">Click</div>";

        // Act
        var result = RegexPatterns.InlineEventHandlerQuoted().IsMatch(html);

        // Assert
        await Assert.That(result).IsTrue();
    }

    #endregion

    #region HTML Clipboard Format Tests

    [Test]
    public async Task StartHtml_WithValidHeader_ExtractsOffset()
    {
        // Arrange
        var header = "Version:0.9\r\nStartHTML:0000000123\r\nEndHTML:0000000456";

        // Act
        var match = RegexPatterns.StartHtml().Match(header);

        // Assert
        await Assert.That(match.Success).IsTrue();
        await Assert.That(match.Groups[1].Value).IsEqualTo("0000000123");
    }

    [Test]
    public async Task EndHtml_WithValidHeader_ExtractsOffset()
    {
        // Arrange
        var header = "Version:0.9\r\nStartHTML:0000000123\r\nEndHTML:0000000456";

        // Act
        var match = RegexPatterns.EndHtml().Match(header);

        // Assert
        await Assert.That(match.Success).IsTrue();
        await Assert.That(match.Groups[1].Value).IsEqualTo("0000000456");
    }

    [Test]
    public async Task StartHtml_CaseInsensitive_Matches()
    {
        // Arrange
        var header = "starthtml:123";

        // Act
        var match = RegexPatterns.StartHtml().Match(header);

        // Assert
        await Assert.That(match.Success).IsTrue();
        await Assert.That(match.Groups[1].Value).IsEqualTo("123");
    }

    #endregion

    #region HtmlTag Tests

    [Test]
    public async Task HtmlTag_WithSimpleTag_Matches()
    {
        // Arrange
        var html = "Hello <b>World</b>";

        // Act
        var result = RegexPatterns.HtmlTag().Replace(html, string.Empty);

        // Assert
        await Assert.That(result).IsEqualTo("Hello World");
    }

    [Test]
    public async Task HtmlTag_WithSelfClosingTag_Matches()
    {
        // Arrange
        var html = "Line break<br/>here";

        // Act
        var result = RegexPatterns.HtmlTag().Replace(html, string.Empty);

        // Assert
        await Assert.That(result).IsEqualTo("Line breakhere");
    }

    [Test]
    public async Task HtmlTag_WithTagAttributes_Matches()
    {
        // Arrange
        var html = "<a href=\"https://example.com\" class=\"link\">Click here</a>";

        // Act
        var result = RegexPatterns.HtmlTag().Replace(html, string.Empty);

        // Assert
        await Assert.That(result).IsEqualTo("Click here");
    }

    [Test]
    public async Task HtmlTag_WithMultipleTags_RemovesAll()
    {
        // Arrange
        var html = "<p>This is <strong>bold</strong> and <em>italic</em> text.</p>";

        // Act
        var result = RegexPatterns.HtmlTag().Replace(html, string.Empty);

        // Assert
        await Assert.That(result).IsEqualTo("This is bold and italic text.");
    }

    [Test]
    public async Task HtmlTag_WithNoTags_ReturnsOriginal()
    {
        // Arrange
        var text = "Plain text with no tags";

        // Act
        var result = RegexPatterns.HtmlTag().Replace(text, string.Empty);

        // Assert
        await Assert.That(result).IsEqualTo(text);
    }

    #endregion

    #region SqlErrorToken Tests

    [Test]
    public async Task SqlErrorToken_WithSqliteError_ExtractsToken()
    {
        // Arrange
        var errorMessage = "SQL logic error: near \"SELCT\" - syntax error";

        // Act
        var match = RegexPatterns.SqlErrorToken().Match(errorMessage);

        // Assert
        await Assert.That(match.Success).IsTrue();
        await Assert.That(match.Groups[1].Value).IsEqualTo("SELCT");
    }

    [Test]
    public async Task SqlErrorToken_WithComplexToken_ExtractsToken()
    {
        // Arrange
        var errorMessage = "Error near \"FROM_TABLE\" in SELECT statement";

        // Act
        var match = RegexPatterns.SqlErrorToken().Match(errorMessage);

        // Assert
        await Assert.That(match.Success).IsTrue();
        await Assert.That(match.Groups[1].Value).IsEqualTo("FROM_TABLE");
    }

    [Test]
    public async Task SqlErrorToken_CaseInsensitive_Matches()
    {
        // Arrange
        var errorMessage = "Error NEAR \"token\" - syntax issue";

        // Act
        var match = RegexPatterns.SqlErrorToken().Match(errorMessage);

        // Assert
        await Assert.That(match.Success).IsTrue();
        await Assert.That(match.Groups[1].Value).IsEqualTo("token");
    }

    [Test]
    public async Task SqlErrorToken_WithNoNearKeyword_DoesNotMatch()
    {
        // Arrange
        var errorMessage = "SQL error: invalid syntax";

        // Act
        var result = RegexPatterns.SqlErrorToken().IsMatch(errorMessage);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task SqlErrorToken_WithNoQuotedToken_DoesNotMatch()
    {
        // Arrange
        var errorMessage = "Error near the end of query";

        // Act
        var result = RegexPatterns.SqlErrorToken().IsMatch(errorMessage);

        // Assert
        await Assert.That(result).IsFalse();
    }

    #endregion
}
