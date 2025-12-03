using ClipMate.Platform;
using Moq;

namespace ClipMate.Tests.Unit.Platform;

/// <summary>
/// Tests for <see cref="IClipboardFormatEnumerator" /> implementations.
/// </summary>
public class ClipboardFormatEnumeratorTests
{
    [Test]
    public async Task GetAllAvailableFormats_ReturnsEmptyList_WhenNoFormatsAvailable()
    {
        // Arrange
        var mockEnumerator = new Mock<IClipboardFormatEnumerator>();
        mockEnumerator.Setup(p => p.GetAllAvailableFormats())
            .Returns(new List<ClipboardFormatInfo>());

        // Act
        var formats = mockEnumerator.Object.GetAllAvailableFormats();

        // Assert
        await Assert.That(formats).IsNotNull();
        await Assert.That(formats.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetAllAvailableFormats_ReturnsStandardFormats_WhenTextOnClipboard()
    {
        // Arrange
        var mockEnumerator = new Mock<IClipboardFormatEnumerator>();
        var expectedFormats = new List<ClipboardFormatInfo>
        {
            new("TEXT", 1),
            new("CF_UNICODETEXT", 13),
            new("LOCALE", 16)
        };

        mockEnumerator.Setup(p => p.GetAllAvailableFormats())
            .Returns(expectedFormats);

        // Act
        var formats = mockEnumerator.Object.GetAllAvailableFormats();

        // Assert
        await Assert.That(formats).IsNotNull();
        await Assert.That(formats.Count).IsEqualTo(3);
        await Assert.That(formats[0].FormatName).IsEqualTo("TEXT");
        await Assert.That(formats[0].FormatCode).IsEqualTo((uint)1);
    }

    [Test]
    public async Task GetAllAvailableFormats_ReturnsCustomFormats_WhenHtmlOnClipboard()
    {
        // Arrange
        var mockEnumerator = new Mock<IClipboardFormatEnumerator>();
        var expectedFormats = new List<ClipboardFormatInfo>
        {
            new("TEXT", 1),
            new("CF_UNICODETEXT", 13),
            new("HTML Format", 49351), // Custom registered format
            new("Rich Text Format", 49408) // Custom registered format
        };

        mockEnumerator.Setup(p => p.GetAllAvailableFormats())
            .Returns(expectedFormats);

        // Act
        var formats = mockEnumerator.Object.GetAllAvailableFormats();

        // Assert
        await Assert.That(formats).IsNotNull();
        await Assert.That(formats.Count).IsEqualTo(4);

        var htmlFormat = formats.FirstOrDefault(f => f.FormatName == "HTML Format");
        await Assert.That(htmlFormat).IsNotNull();
        await Assert.That(htmlFormat!.FormatCode).IsGreaterThan((uint)16); // Custom formats have codes > 16
    }

    [Test]
    public async Task GetAllAvailableFormats_PreservesFormatNameCasing_FromWindowsApi()
    {
        // Arrange - Windows API returns exact format names with specific casing
        var mockEnumerator = new Mock<IClipboardFormatEnumerator>();
        var expectedFormats = new List<ClipboardFormatInfo>
        {
            new("HTML Format", 49351), // Note: "Format" is capitalized
            new("dopus_cf_sourcethread", 50123), // Note: all lowercase with underscores
            new("Preferred DropEffect", 50124) // Note: mixed case with space
        };

        mockEnumerator.Setup(p => p.GetAllAvailableFormats())
            .Returns(expectedFormats);

        // Act
        var formats = mockEnumerator.Object.GetAllAvailableFormats();

        // Assert
        await Assert.That(formats[0].FormatName).IsEqualTo("HTML Format"); // Exact casing
        await Assert.That(formats[1].FormatName).IsEqualTo("dopus_cf_sourcethread"); // Exact casing
        await Assert.That(formats[2].FormatName).IsEqualTo("Preferred DropEffect"); // Exact casing with space
    }

    [Test]
    public async Task GetAllAvailableFormats_HandlesLargeNumberOfFormats_FromExcel()
    {
        // Arrange - Excel can provide 21+ formats
        var mockEnumerator = new Mock<IClipboardFormatEnumerator>();
        var manyFormats = new List<ClipboardFormatInfo>();

        for (uint i = 1; i <= 25; i++)
            manyFormats.Add(new ClipboardFormatInfo($"Format_{i}", i));

        mockEnumerator.Setup(p => p.GetAllAvailableFormats())
            .Returns(manyFormats);

        // Act
        var formats = mockEnumerator.Object.GetAllAvailableFormats();

        // Assert
        await Assert.That(formats.Count).IsEqualTo(25);
        await Assert.That(formats.All(f => !string.IsNullOrEmpty(f.FormatName))).IsTrue();
    }
}
