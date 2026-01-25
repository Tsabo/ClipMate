using System.Reflection;
using ClipMate.Data.Services;

namespace ClipMate.Tests.Unit.Services;

public partial class ClipAppendServiceTests
{
    [Test]
    [Category("ProcessEscapeSequences")]
    public async Task ProcessEscapeSequences_WithNewlineEscape_ReplacesWithNewline()
    {
        // Arrange
        var method = GetPrivateStaticMethod("ProcessEscapeSequences");

        // Act
        var result = (string)method.Invoke(null, ["Hello\\nWorld"])!;

        // Assert
        await Assert.That(result).IsEqualTo("Hello\nWorld");
    }

    [Test]
    [Category("ProcessEscapeSequences")]
    public async Task ProcessEscapeSequences_WithTabEscape_ReplacesWithTab()
    {
        // Arrange
        var method = GetPrivateStaticMethod("ProcessEscapeSequences");

        // Act
        var result = (string)method.Invoke(null, ["Hello\\tWorld"])!;

        // Assert
        await Assert.That(result).IsEqualTo("Hello\tWorld");
    }

    [Test]
    [Category("ProcessEscapeSequences")]
    public async Task ProcessEscapeSequences_WithCarriageReturnEscape_ReplacesWithCarriageReturn()
    {
        // Arrange
        var method = GetPrivateStaticMethod("ProcessEscapeSequences");

        // Act
        var result = (string)method.Invoke(null, ["Hello\\rWorld"])!;

        // Assert
        await Assert.That(result).IsEqualTo("Hello\rWorld");
    }

    [Test]
    [Category("ProcessEscapeSequences")]
    public async Task ProcessEscapeSequences_WithMultipleEscapes_ReplacesAll()
    {
        // Arrange
        var method = GetPrivateStaticMethod("ProcessEscapeSequences");

        // Act
        var result = (string)method.Invoke(null, ["Line1\\nLine2\\tTabbed\\rReturn"])!;

        // Assert
        await Assert.That(result).IsEqualTo("Line1\nLine2\tTabbed\rReturn");
    }

    [Test]
    [Category("ProcessEscapeSequences")]
    public async Task ProcessEscapeSequences_WithEmptyString_ReturnsEmpty()
    {
        // Arrange
        var method = GetPrivateStaticMethod("ProcessEscapeSequences");

        // Act
        var result = (string)method.Invoke(null, [""])!;

        // Assert
        await Assert.That(result).IsEqualTo(string.Empty);
    }

    [Test]
    [Category("StripTrailingLineBreaks")]
    public async Task StripTrailingLineBreaks_WithTrailingNewline_RemovesIt()
    {
        // Arrange
        var method = GetPrivateStaticMethod("StripTrailingLineBreaks");

        // Act
        var result = (string)method.Invoke(null, ["Hello World\n"])!;

        // Assert
        await Assert.That(result).IsEqualTo("Hello World");
    }

    [Test]
    [Category("StripTrailingLineBreaks")]
    public async Task StripTrailingLineBreaks_WithTrailingCRLF_RemovesIt()
    {
        // Arrange
        var method = GetPrivateStaticMethod("StripTrailingLineBreaks");

        // Act
        var result = (string)method.Invoke(null, ["Hello World\r\n"])!;

        // Assert
        await Assert.That(result).IsEqualTo("Hello World");
    }

    [Test]
    [Category("StripTrailingLineBreaks")]
    public async Task StripTrailingLineBreaks_WithMultipleTrailingBreaks_RemovesAll()
    {
        // Arrange
        var method = GetPrivateStaticMethod("StripTrailingLineBreaks");

        // Act
        var result = (string)method.Invoke(null, ["Hello World\n\n\n"])!;

        // Assert
        await Assert.That(result).IsEqualTo("Hello World");
    }

    [Test]
    [Category("StripTrailingLineBreaks")]
    public async Task StripTrailingLineBreaks_WithNoTrailingBreaks_ReturnsUnchanged()
    {
        // Arrange
        var method = GetPrivateStaticMethod("StripTrailingLineBreaks");

        // Act
        var result = (string)method.Invoke(null, ["Hello World"])!;

        // Assert
        await Assert.That(result).IsEqualTo("Hello World");
    }

    [Test]
    [Category("ComputeContentHash")]
    public async Task ComputeContentHash_WithSameText_ReturnsSameHash()
    {
        // Arrange
        var method = GetPrivateStaticMethod("ComputeContentHash");
        const string text = "Hello World";

        // Act
        var hash1 = (string)method.Invoke(null, [text])!;
        var hash2 = (string)method.Invoke(null, [text])!;

        // Assert
        await Assert.That(hash1).IsEqualTo(hash2);
    }

    [Test]
    [Category("ComputeContentHash")]
    public async Task ComputeContentHash_WithDifferentText_ReturnsDifferentHash()
    {
        // Arrange
        var method = GetPrivateStaticMethod("ComputeContentHash");

        // Act
        var hash1 = (string)method.Invoke(null, ["Hello World"])!;
        var hash2 = (string)method.Invoke(null, ["Goodbye World"])!;

        // Assert
        await Assert.That(hash1).IsNotEqualTo(hash2);
    }

    [Test]
    [Category("ComputeContentHash")]
    public async Task ComputeContentHash_ReturnsHexString()
    {
        // Arrange
        var method = GetPrivateStaticMethod("ComputeContentHash");

        // Act
        var hash = (string)method.Invoke(null, ["Hello World"])!;

        // Assert - SHA-256 produces 64 hex characters
        await Assert.That(hash).Length().IsEqualTo(64);
        await Assert.That(hash).Matches("^[A-F0-9]+$"); // All uppercase hex
    }

    [Test]
    [Category("ComputeChecksum")]
    public async Task ComputeChecksum_WithSameText_ReturnsSameChecksum()
    {
        // Arrange
        var method = GetPrivateStaticMethod("ComputeChecksum");
        const string text = "Hello World";

        // Act
        var checksum1 = (int)method.Invoke(null, [text])!;
        var checksum2 = (int)method.Invoke(null, [text])!;

        // Assert
        await Assert.That(checksum1).IsEqualTo(checksum2);
    }

    [Test]
    [Category("ComputeChecksum")]
    public async Task ComputeChecksum_WithDifferentText_ReturnsDifferentChecksum()
    {
        // Arrange
        var method = GetPrivateStaticMethod("ComputeChecksum");

        // Act
        var checksum1 = (int)method.Invoke(null, ["Hello World"])!;
        var checksum2 = (int)method.Invoke(null, ["Goodbye World"])!;

        // Assert
        await Assert.That(checksum1).IsNotEqualTo(checksum2);
    }

    private static MethodInfo GetPrivateStaticMethod(string methodName)
    {
        var method = typeof(ClipAppendService).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static);

        if (method == null)
            throw new InvalidOperationException($"Method {methodName} not found");

        return method;
    }
}
