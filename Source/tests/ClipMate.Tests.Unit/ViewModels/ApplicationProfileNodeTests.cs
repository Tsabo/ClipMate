using ClipMate.App.Models.TreeNodes;
using ClipMate.App.ViewModels;
using ClipMate.Core.Models;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Unit tests for ApplicationProfileNode ViewModel.
/// </summary>
[Category("ApplicationProfileNode")]
[Category("ViewModel")]
public class ApplicationProfileNodeTests : TestFixtureBase
{
    [Test]
    public async Task Constructor_WithValidProfile_ShouldInitializeProperties()
    {
        // Arrange
        var profile = ApplicationProfileTestFixtures.GetNotepadProfile();

        // Act
        var node = new ApplicationProfileNode(profile);

        // Assert
        await Assert.That(node.Profile).IsEqualTo(profile);
        await Assert.That(node.Name).IsEqualTo("NOTEPAD");
        await Assert.That(node.Enabled).IsTrue();
        await Assert.That(node.Icon).IsEqualTo("🖥️");
        await Assert.That(node.NodeType).IsEqualTo(TreeNodeType.ApplicationProfile);
    }

    [Test]
    public async Task Constructor_WithNullProfile_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => new ApplicationProfileNode(null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_ShouldPopulateChildrenWithFormats()
    {
        // Arrange
        var profile = ApplicationProfileTestFixtures.GetNotepadProfile();

        // Act
        var node = new ApplicationProfileNode(profile);

        // Assert
        await Assert.That(node.Children.Count).IsEqualTo(profile.Formats.Count);
        await Assert.That(node.Children.All(p => p is ApplicationProfileFormatNode)).IsTrue();
        await Assert.That(node.Children.All(p => p.Parent == node)).IsTrue();
    }

    [Test]
    public async Task Constructor_ShouldSortFormatsAlphabetically()
    {
        // Arrange
        var profile = new ApplicationProfile
        {
            ApplicationName = "TEST",
            Enabled = true,
            Formats = new Dictionary<string, bool>
            {
                ["ZEBRA"] = true,
                ["ALPHA"] = false,
                ["BETA"] = true,
            },
        };

        // Act
        var node = new ApplicationProfileNode(profile);

        // Assert
        var formatNames = node.Children.Cast<ApplicationProfileFormatNode>()
            .Select(p => p.FormatName)
            .ToList();

        await Assert.That(formatNames[0]).IsEqualTo("ALPHA");
        await Assert.That(formatNames[1]).IsEqualTo("BETA");
        await Assert.That(formatNames[2]).IsEqualTo("ZEBRA");
    }

    [Test]
    public async Task Enabled_WhenChanged_ShouldUpdateUnderlyingProfile()
    {
        // Arrange
        var profile = ApplicationProfileTestFixtures.GetNotepadProfile();
        var node = new ApplicationProfileNode(profile);

        // Act
        node.Enabled = false;

        // Assert
        await Assert.That(node.Enabled).IsFalse();
        await Assert.That(profile.Enabled).IsFalse();
    }

    [Test]
    public async Task Enabled_WhenToggled_ShouldRaisePropertyChanged()
    {
        // Arrange
        var profile = ApplicationProfileTestFixtures.GetNotepadProfile();
        var node = new ApplicationProfileNode(profile);
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
    }

    [Test]
    public async Task Children_ShouldContainCorrectFormatNodes()
    {
        // Arrange
        var profile = ApplicationProfileTestFixtures.GetChromeProfile();

        // Act
        var node = new ApplicationProfileNode(profile);

        // Assert
        var formatNode = node.Children.Cast<ApplicationProfileFormatNode>()
            .First(f => f.FormatName == "HTML Format");

        await Assert.That(formatNode).IsNotNull();
        await Assert.That(formatNode.Enabled).IsTrue();
        await Assert.That(formatNode.Parent).IsEqualTo(node);
    }
}
