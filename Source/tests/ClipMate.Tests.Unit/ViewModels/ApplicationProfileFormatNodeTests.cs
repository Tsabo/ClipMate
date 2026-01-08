using ClipMate.App.Models.TreeNodes;
using ClipMate.App.ViewModels;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Unit tests for ApplicationProfileFormatNode ViewModel.
/// </summary>
[Category("ApplicationProfileFormatNode")]
[Category("ViewModel")]
public class ApplicationProfileFormatNodeTests : TestFixtureBase
{
    [Test]
    public async Task Constructor_WithValidParameters_ShouldInitializeProperties()
    {
        // Arrange & Act
        var node = new ApplicationProfileFormatNode("CF_UNICODETEXT", true);

        // Assert
        await Assert.That(node.FormatName).IsEqualTo("CF_UNICODETEXT");
        await Assert.That(node.Enabled).IsTrue();
        await Assert.That(node.NodeType).IsEqualTo(TreeNodeType.ApplicationProfileFormat);
    }

    [Test]
    public async Task Constructor_WithNullFormatName_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => new ApplicationProfileFormatNode(null!, true))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Name_ShouldIncludeFormatNameAndDescription()
    {
        // Arrange
        var node = new ApplicationProfileFormatNode("CF_UNICODETEXT", true);

        // Act
        var name = node.Name;

        // Assert
        await Assert.That(name).Contains("CF_UNICODETEXT");
        await Assert.That(name).Contains("Plain Text (Unicode)");
    }

    [Test]
    public async Task Enabled_WhenChanged_ShouldRaisePropertyChanged()
    {
        // Arrange
        var node = new ApplicationProfileFormatNode("CF_TEXT", true);
        var propertyChanged = false;
        node.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(node.Enabled))
                propertyChanged = true;
        };

        // Act
        node.Enabled = false;

        // Assert
        await Assert.That(propertyChanged).IsTrue();
        await Assert.That(node.Enabled).IsFalse();
    }

    [Test]
    [Arguments("CF_TEXT", "📝")]
    [Arguments("CF_UNICODETEXT", "📝")]
    [Arguments("TEXT", "📝")]
    [Arguments("CF_BITMAP", "🖼️")]
    [Arguments("BITMAP", "🖼️")]
    [Arguments("CF_DIB", "🖼️")]
    [Arguments("CF_HDROP", "📁")]
    [Arguments("HDROP", "📁")]
    [Arguments("HTML FORMAT", "🌐")]
    [Arguments("CF_HTML", "🌐")]
    [Arguments("RICH TEXT FORMAT", "📄")]
    [Arguments("RTF", "📄")]
    [Arguments("CF_RTF", "📄")]
    [Arguments("UNKNOWN_FORMAT", "📋")]
    public async Task GetFormatIcon_ShouldReturnCorrectIcon(string formatName, string expectedIcon)
    {
        // Act
        var icon = ApplicationProfileFormatNode.GetFormatIcon(formatName);

        // Assert
        await Assert.That(icon).IsEqualTo(expectedIcon);
    }

    [Test]
    [Arguments("CF_TEXT", "Plain Text (ANSI)")]
    [Arguments("TEXT", "Plain Text (ANSI)")]
    [Arguments("CF_UNICODETEXT", "Plain Text (Unicode)")]
    [Arguments("CF_BITMAP", "Bitmap Image")]
    [Arguments("BITMAP", "Bitmap Image")]
    [Arguments("CF_DIB", "Bitmap Image")]
    [Arguments("CF_HDROP", "File Drop List")]
    [Arguments("HDROP", "File Drop List")]
    [Arguments("HTML FORMAT", "HTML Markup")]
    [Arguments("CF_HTML", "HTML Markup")]
    [Arguments("RICH TEXT FORMAT", "Rich Text Format")]
    [Arguments("RTF", "Rich Text Format")]
    [Arguments("CF_RTF", "Rich Text Format")]
    [Arguments("CF_WAVE", "Audio Waveform")]
    [Arguments("CF_ENHMETAFILE", "Enhanced Metafile")]
    [Arguments("CF_METAFILEPICT", "Windows Metafile")]
    [Arguments("DATAOBJECT", "Serialized Data Object")]
    [Arguments("OLEOBJECT", "OLE Object")]
    [Arguments("OLEPRIVATEDATA", "OLE Private Data")]
    [Arguments("CF_LOCALE", "Locale Information")]
    [Arguments("LOCALE", "Locale Information")]
    [Arguments("UNKNOWN_FORMAT", "Custom Format")]
    public async Task GetFormatDescription_ShouldReturnCorrectDescription(string formatName, string expectedDescription)
    {
        // Act
        var description = ApplicationProfileFormatNode.GetFormatDescription(formatName);

        // Assert
        await Assert.That(description).IsEqualTo(expectedDescription);
    }

    [Test]
    public async Task Icon_ShouldUseGetFormatIcon()
    {
        // Arrange
        var node = new ApplicationProfileFormatNode("HTML FORMAT", true);

        // Act
        var icon = node.Icon;

        // Assert
        await Assert.That(icon).IsEqualTo("🌐");
    }

    [Test]
    public async Task GetFormatIcon_WithNullFormatName_ShouldReturnDefaultIcon()
    {
        // Act
        var icon = ApplicationProfileFormatNode.GetFormatIcon(null!);

        // Assert
        await Assert.That(icon).IsEqualTo("📋");
    }

    [Test]
    public async Task GetFormatDescription_WithNullFormatName_ShouldReturnCustomFormat()
    {
        // Act
        var description = ApplicationProfileFormatNode.GetFormatDescription(null!);

        // Assert
        await Assert.That(description).IsEqualTo("Custom Format");
    }

    [Test]
    public async Task GetFormatIcon_ShouldBeCaseInsensitive()
    {
        // Arrange & Act
        var upperIcon = ApplicationProfileFormatNode.GetFormatIcon("HTML FORMAT");
        var lowerIcon = ApplicationProfileFormatNode.GetFormatIcon("html format");
        var mixedIcon = ApplicationProfileFormatNode.GetFormatIcon("Html Format");

        // Assert
        await Assert.That(upperIcon).IsEqualTo("🌐");
        await Assert.That(lowerIcon).IsEqualTo("🌐");
        await Assert.That(mixedIcon).IsEqualTo("🌐");
    }

    [Test]
    public async Task GetFormatDescription_ShouldBeCaseInsensitive()
    {
        // Arrange & Act
        var upperDesc = ApplicationProfileFormatNode.GetFormatDescription("CF_UNICODETEXT");
        var lowerDesc = ApplicationProfileFormatNode.GetFormatDescription("cf_unicodetext");
        var mixedDesc = ApplicationProfileFormatNode.GetFormatDescription("Cf_UnicodeText");

        // Assert
        await Assert.That(upperDesc).IsEqualTo("Plain Text (Unicode)");
        await Assert.That(lowerDesc).IsEqualTo("Plain Text (Unicode)");
        await Assert.That(mixedDesc).IsEqualTo("Plain Text (Unicode)");
    }
}
