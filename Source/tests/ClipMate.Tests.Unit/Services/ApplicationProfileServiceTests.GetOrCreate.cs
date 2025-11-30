using ClipMate.Core.Models;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for GetOrCreateProfileAsync in <see cref="IApplicationProfileService" />.
/// </summary>
[Category("ApplicationProfileService")]
[Category("GetOrCreate")]
public class ApplicationProfileServiceGetOrCreateTests : ApplicationProfileServiceTestsBase
{
    [Test]
    public async Task GetOrCreateProfileAsync_ReturnsExistingProfile_WhenExists()
    {
        // Arrange
        var existingProfile = ApplicationProfileTestFixtures.GetNotepadProfile();
        MockStore.Setup(p => p.GetProfileAsync("NOTEPAD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingProfile);

        // Act
        var result = await Service.GetOrCreateProfileAsync("notepad.exe");

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.ApplicationName).IsEqualTo("NOTEPAD");
        await Assert.That(result.Formats["TEXT"]).IsTrue();
        MockStore.Verify(p => p.AddOrUpdateProfileAsync(It.IsAny<ApplicationProfile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GetOrCreateProfileAsync_CreatesNewProfile_WhenNotExists()
    {
        // Arrange
        MockStore.Setup(p => p.GetProfileAsync("NOTEPAD", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApplicationProfile?)null);

        MockStore.Setup(p => p.AddOrUpdateProfileAsync(It.IsAny<ApplicationProfile>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Service.GetOrCreateProfileAsync("notepad.exe");

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.ApplicationName).IsEqualTo("NOTEPAD");
        await Assert.That(result.Enabled).IsTrue();

        // Verify smart defaults
        await Assert.That(result.Formats["TEXT"]).IsTrue();
        await Assert.That(result.Formats["CF_UNICODETEXT"]).IsTrue();
        await Assert.That(result.Formats["BITMAP"]).IsTrue();
        await Assert.That(result.Formats["HDROP"]).IsTrue();
        await Assert.That(result.Formats["HTML Format"]).IsTrue();
        await Assert.That(result.Formats["Rich Text Format"]).IsFalse();
        await Assert.That(result.Formats["DataObject"]).IsFalse();
        await Assert.That(result.Formats["LOCALE"]).IsFalse();
        await Assert.That(result.Formats["OlePrivateData"]).IsFalse();

        MockStore.Verify(p => p.AddOrUpdateProfileAsync(It.Is<ApplicationProfile>(s => s.ApplicationName == "NOTEPAD"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetOrCreateProfileAsync_NormalizesApplicationName()
    {
        // Arrange
        MockStore.Setup(p => p.GetProfileAsync("CHROME", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApplicationProfile?)null);

        MockStore.Setup(p => p.AddOrUpdateProfileAsync(It.IsAny<ApplicationProfile>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Service.GetOrCreateProfileAsync("chrome.exe");

        // Assert
        await Assert.That(result.ApplicationName).IsEqualTo("CHROME");
        MockStore.Verify(p => p.GetProfileAsync("CHROME", It.IsAny<CancellationToken>()), Times.Once);
    }
}
